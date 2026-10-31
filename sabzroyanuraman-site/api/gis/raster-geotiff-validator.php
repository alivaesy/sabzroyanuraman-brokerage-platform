<?php
declare(strict_types=1);

/**
 * Structural GeoTIFF validator for classic TIFF (BigTIFF is intentionally rejected).
 * Throws a stable error code; the API maps it to HTTP 415.
 */
function raster_u16(string $bytes, int $offset, bool $littleEndian): int {
    if ($offset < 0 || $offset + 2 > strlen($bytes)) {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    $value = unpack($littleEndian ? 'v' : 'n', substr($bytes, $offset, 2));
    return (int)$value[1];
}

function raster_u32(string $bytes, int $offset, bool $littleEndian): int {
    if ($offset < 0 || $offset + 4 > strlen($bytes)) {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    $value = unpack($littleEndian ? 'V' : 'N', substr($bytes, $offset, 4));
    return (int)$value[1];
}

function raster_validate_geotiff(string $path, array $metadata): void {
    $size = filesize($path);
    if ($size === false || $size < 14) {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    $header = file_get_contents($path, false, null, 0, 8);
    if (strlen($header) !== 8) {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    $littleEndian = substr($header, 0, 2) === 'II';
    if (!$littleEndian && substr($header, 0, 2) !== 'MM') {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    if (raster_u16($header, 2, $littleEndian) !== 42) {
        throw new RuntimeException('unsupported_or_invalid_tiff_variant');
    }

    $ifdOffset = raster_u32($header, 4, $littleEndian);
    if ($ifdOffset < 8 || $ifdOffset + 2 > $size) {
        throw new RuntimeException('invalid_geotiff_ifd');
    }
    $countBytes = file_get_contents($path, false, null, $ifdOffset, 2);
    $entryCount = raster_u16($countBytes === false ? '' : $countBytes, 0, $littleEndian);
    if ($entryCount < 1 || $entryCount > 4096 || $ifdOffset + 2 + ($entryCount * 12) + 4 > $size) {
        throw new RuntimeException('invalid_geotiff_ifd');
    }

    $handle = fopen($path, 'rb');
    if ($handle === false) {
        throw new RuntimeException('invalid_geotiff_structure');
    }
    $typeSizes = [1=>1,2=>1,3=>2,4=>4,5=>8,6=>1,7=>1,8=>2,9=>4,10=>8,11=>4,12=>8];
    $entries = [];
    for ($index = 0; $index < $entryCount; $index++) {
        fseek($handle, $ifdOffset + 2 + ($index * 12));
        $entry = fread($handle, 12);
        if (strlen($entry) !== 12) {
            fclose($handle);
            throw new RuntimeException('invalid_geotiff_ifd');
        }
        $tag = raster_u16($entry, 0, $littleEndian);
        $type = raster_u16($entry, 2, $littleEndian);
        $valueCount = raster_u32($entry, 4, $littleEndian);
        if (!isset($typeSizes[$type]) || $valueCount < 1 || $valueCount > 100000000) {
            fclose($handle);
            throw new RuntimeException('invalid_geotiff_tag');
        }
        $byteCount = $typeSizes[$type] * $valueCount;
        $valueOffset = $byteCount <= 4
            ? ($ifdOffset + 2 + ($index * 12) + 8)
            : raster_u32($entry, 8, $littleEndian);
        if ($valueOffset < 0 || $valueOffset + $byteCount > $size) {
            fclose($handle);
            throw new RuntimeException('invalid_geotiff_tag_offset');
        }
        $entries[$tag] = ['type'=>$type, 'count'=>$valueCount, 'pos'=>$valueOffset, 'bytes'=>$byteCount];
    }

    $integerValues = static function (int $tag) use ($entries, $handle, $littleEndian): array {
        if (!isset($entries[$tag])) {
            return [];
        }
        $entry = $entries[$tag];
        $step = $entry['type'] === 3 ? 2 : ($entry['type'] === 4 ? 4 : 0);
        if ($step === 0) {
            return [];
        }
        fseek($handle, $entry['pos']);
        $raw = fread($handle, $entry['bytes']);
        if ($raw === false || strlen($raw) !== $entry['bytes']) {
            return [];
        }
        $values = [];
        for ($i = 0; $i < $entry['count']; $i++) {
            $values[] = $step === 2
                ? raster_u16($raw, $i * 2, $littleEndian)
                : raster_u32($raw, $i * 4, $littleEndian);
        }
        return $values;
    };

    $width = $integerValues(256)[0] ?? 0;
    $height = $integerValues(257)[0] ?? 0;
    $offsets = $integerValues(273);
    $lengths = $integerValues(279);
    if (!$offsets || !$lengths) {
        $offsets = $integerValues(324);
        $lengths = $integerValues(325);
    }

    $hasTransform = isset($entries[34264])
        && $entries[34264]['type'] === 12
        && $entries[34264]['count'] >= 16;
    $hasTieScale = isset($entries[33922], $entries[33550])
        && $entries[33922]['type'] === 12
        && $entries[33922]['count'] >= 6
        && $entries[33550]['type'] === 12
        && $entries[33550]['count'] >= 3;

    $geoKeys = $integerValues(34735);
    $tiffEpsg = 0;
    if (count($geoKeys) >= 4) {
        $keyCount = min((int)$geoKeys[3], 256);
        for ($key = 0; $key < $keyCount; $key++) {
            $base = 4 + ($key * 4);
            if (!isset($geoKeys[$base + 3])) {
                break;
            }
            if (in_array($geoKeys[$base], [2048, 3072], true)
                && $geoKeys[$base + 1] === 0
                && $geoKeys[$base + 2] === 1) {
                $tiffEpsg = (int)$geoKeys[$base + 3];
                break;
            }
        }
    }

    $valid = $width >= 1 && $height >= 1
        && $width <= 1000000 && $height <= 1000000
        && $width * $height <= 100000000
        && $width === (int)$metadata['width']
        && $height === (int)$metadata['height']
        && $tiffEpsg === (int)$metadata['crs']['epsg']
        && isset($entries[34735])
        && ($hasTransform || $hasTieScale)
        && $offsets && count($offsets) === count($lengths);

    if (!$valid) {
        fclose($handle);
        throw new RuntimeException('geotiff_metadata_or_image_structure_invalid');
    }
    foreach ($offsets as $index => $offset) {
        if (($lengths[$index] ?? 0) < 1 || $offset < 8 || $offset + $lengths[$index] > $size) {
            fclose($handle);
            throw new RuntimeException('geotiff_image_block_out_of_bounds');
        }
    }
    fclose($handle);
}
