<?php
declare(strict_types=1);

/**
 * Commits an immutable content-addressed TIFF, then atomically switches its JSON pointer.
 * A failed pointer rename rolls back only the newly-created blob and preserves the old asset.
 * $rename is injectable for deterministic failure-injection tests.
 */
function raster_commit_content_addressed(
    string $directory,
    string $assetId,
    string $stagedFile,
    string $metadataFile,
    array $metadata,
    ?array $previousMetadata = null,
    ?callable $rename = null
): array {
    $rename ??= static fn(string $from, string $to): bool => rename($from, $to);
    $hash = hash_file('sha256', $stagedFile);
    if ($hash === false) {
        @unlink($stagedFile);
        throw new RuntimeException('raster_storage_failed');
    }
    $storedName = $assetId . '.' . $hash . '.tif';
    $storedPath = $directory . '/' . $storedName;
    $previousName = is_array($previousMetadata) && isset($previousMetadata['storage_file'])
        ? (string)$previousMetadata['storage_file']
        : '';
    $metadata['storage_file'] = $storedName;
    $metadata['sha256'] = $hash;
    $metadataTemp = $metadataFile . '.' . bin2hex(random_bytes(6)) . '.tmp';
    $encoded = json_encode($metadata, JSON_UNESCAPED_SLASHES | JSON_UNESCAPED_UNICODE);
    if ($encoded === false || file_put_contents($metadataTemp, $encoded, LOCK_EX) === false) {
        @unlink($stagedFile);
        @unlink($metadataTemp);
        throw new RuntimeException('raster_storage_failed');
    }

    $createdBlob = false;
    if (is_file($storedPath)) {
        @unlink($stagedFile);
    } else {
        if (!$rename($stagedFile, $storedPath)) {
            @unlink($stagedFile);
            @unlink($metadataTemp);
            throw new RuntimeException('raster_storage_failed');
        }
        $createdBlob = true;
    }

    // This same-filesystem rename is the transaction's single commit point.
    if (!$rename($metadataTemp, $metadataFile)) {
        @unlink($metadataTemp);
        if ($previousName !== $storedName) {
            @unlink($storedPath);
        }
        throw new RuntimeException('raster_storage_failed');
    }

    if ($previousName !== '' && $previousName !== $storedName
        && basename($previousName) === $previousName
        && preg_match('/^[a-zA-Z0-9._-]+\.tif$/', $previousName)) {
        @unlink($directory . '/' . $previousName);
    }
    return $metadata;
}
