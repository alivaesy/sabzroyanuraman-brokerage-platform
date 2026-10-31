<?php
declare(strict_types=1);

require_once __DIR__ . '/../sabzroyanuraman-site/api/gis/raster-geotiff-validator.php';
require_once __DIR__ . '/../sabzroyanuraman-site/api/gis/raster-asset-commit.php';

function check(bool $condition, string $message): void {
    if (!$condition) {
        fwrite(STDERR, "FAIL: {$message}\n");
        exit(1);
    }
}

function make_geotiff_fixture(int $epsg = 32638): string {
    $ifdOffset = 8;
    $entryCount = 12;
    $ifdEnd = $ifdOffset + 2 + ($entryCount * 12) + 4;
    $pixelOffset = $ifdEnd;
    $scaleOffset = $pixelOffset + 2;
    $tieOffset = $scaleOffset + 24;
    $keysOffset = $tieOffset + 48;

    $entry = static function (int $tag, int $type, int $count, string $value): string {
        if (strlen($value) < 4) {
            $value = str_pad($value, 4, "\0");
        }
        return pack('vvV', $tag, $type, $count) . substr($value, 0, 4);
    };
    $entries = [
        $entry(256, 4, 1, pack('V', 1)),
        $entry(257, 4, 1, pack('V', 1)),
        $entry(258, 3, 1, pack('v', 8)),
        $entry(259, 3, 1, pack('v', 1)),
        $entry(262, 3, 1, pack('v', 1)),
        $entry(273, 4, 1, pack('V', $pixelOffset)),
        $entry(277, 3, 1, pack('v', 1)),
        $entry(278, 4, 1, pack('V', 1)),
        $entry(279, 4, 1, pack('V', 1)),
        $entry(33550, 12, 3, pack('V', $scaleOffset)),
        $entry(33922, 12, 6, pack('V', $tieOffset)),
        $entry(34735, 3, 8, pack('V', $keysOffset)),
    ];
    $header = 'II' . pack('vV', 42, $ifdOffset);
    $ifd = pack('v', $entryCount) . implode('', $entries) . pack('V', 0);
    $payload = "\x7f\0"
        . pack('e*', 1.0, 1.0, 0.0)
        . pack('e*', 0.0, 0.0, 0.0, 47.0, 34.0, 0.0)
        . pack('v*', 1, 1, 0, 1, 3072, 0, 1, $epsg);
    return $header . $ifd . $payload;
}

$metadata = ['width'=>1, 'height'=>1, 'crs'=>['epsg'=>32638]];
$path = tempnam(sys_get_temp_dir(), 'sabz-geotiff-');
check($path !== false, 'temporary file should be created');

try {
    file_put_contents($path, make_geotiff_fixture());
    raster_validate_geotiff($path, $metadata);
    echo "PASS: valid GeoTIFF fixture accepted\n";

    file_put_contents($path, "not a TIFF");
    $rejected = false;
    try {
        raster_validate_geotiff($path, $metadata);
    } catch (RuntimeException $error) {
        $rejected = $error->getMessage() === 'invalid_geotiff_structure';
    }
    check($rejected, 'invalid header must be rejected');
    echo "PASS: invalid TIFF header rejected\n";

    file_put_contents($path, make_geotiff_fixture(4326));
    $rejected = false;
    try {
        raster_validate_geotiff($path, $metadata);
    } catch (RuntimeException $error) {
        $rejected = $error->getMessage() === 'geotiff_metadata_or_image_structure_invalid';
    }
    check($rejected, 'GeoKey EPSG mismatch must be rejected');
    echo "PASS: mismatched EPSG metadata rejected\n";

    $truncated = substr(make_geotiff_fixture(), 0, -10);
    file_put_contents($path, $truncated);
    $rejected = false;
    try {
        raster_validate_geotiff($path, $metadata);
    } catch (RuntimeException $error) {
        $rejected = true;
    }
    check($rejected, 'truncated GeoTIFF must be rejected');
    echo "PASS: truncated GeoTIFF rejected\n";

    $directory = sys_get_temp_dir() . '/sabz-raster-commit-' . bin2hex(random_bytes(5));
    mkdir($directory, 0700, true);
    try {
        $metadataFile = $directory . '/asset.json';
        $oldName = 'asset.previous.tif';
        $oldPath = $directory . '/' . $oldName;
        file_put_contents($oldPath, 'previous-raster');
        $oldMetadata = ['asset_id'=>'asset', 'storage_file'=>$oldName, 'sha256'=>'previous'];
        $oldJson = json_encode($oldMetadata);
        file_put_contents($metadataFile, $oldJson);
        $stage = $directory . '/stage-failed.tmp';
        file_put_contents($stage, 'new-raster-content');
        $newHash = hash_file('sha256', $stage);
        $newPath = $directory . '/asset.' . $newHash . '.tif';
        $failMetadataRename = static function (string $from, string $to) use ($metadataFile): bool {
            if ($to === $metadataFile) {
                return false;
            }
            return rename($from, $to);
        };
        $failed = false;
        try {
            raster_commit_content_addressed($directory, 'asset', $stage, $metadataFile,
                ['asset_id'=>'asset', 'file_name'=>'new.tif', 'size'=>18], $oldMetadata, $failMetadataRename);
        } catch (RuntimeException $error) {
            $failed = $error->getMessage() === 'raster_storage_failed';
        }
        check($failed, 'metadata rename failure must fail the commit');
        check(file_get_contents($metadataFile) === $oldJson, 'old metadata pointer must remain unchanged after failure');
        check(is_file($oldPath), 'old raster must remain available after failed commit');
        check(!file_exists($newPath), 'new raster blob must be rolled back after failed metadata commit');
        echo "PASS: metadata commit failure rolls back new blob and preserves old asset\n";

        $stage = $directory . '/stage-success.tmp';
        file_put_contents($stage, 'replacement-raster');
        $newMetadata = raster_commit_content_addressed($directory, 'asset', $stage, $metadataFile,
            ['asset_id'=>'asset', 'file_name'=>'replacement.tif', 'size'=>18], $oldMetadata);
        check(is_file($directory . '/' . $newMetadata['storage_file']), 'new content-addressed raster must exist after commit');
        check(!is_file($oldPath), 'old raster must be removed only after metadata commit succeeds');
        $saved = json_decode((string)file_get_contents($metadataFile), true);
        check(($saved['storage_file'] ?? '') === $newMetadata['storage_file'], 'metadata pointer must identify committed raster');
        echo "PASS: successful metadata commit switches pointer and retires old raster\n";
    } finally {
        foreach (glob($directory . '/*') ?: [] as $item) {
            if (is_file($item)) {
                @unlink($item);
            }
        }
        @rmdir($directory);
    }
} finally {
    @unlink($path);
}
