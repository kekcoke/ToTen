# Implementation Plan: Manifests Vertical Slice
**Date**: 2026-05-29
**POC**: AdaL (Backend Agent)

## 1. Overview
Implement the Manifest vertical slice to manage moving manifests, associate boxes, and generate QR codes stored in Azure Blob Storage.

## 2. Components to Implement
### Infrastructure: QR Code & Storage
- **IStorageService**: Abstraction for uploading files to Azure Blob Storage.
- **IQRCodeService**: Service to generate QR codes (using a library like `QRCoder`) and save them via `IStorageService`.

### Features/Manifests/CreateManifest
- **Endpoint**: `POST /api/manifests`
- **Logic**: Create a `Manifest` record with source/destination, associate it with the user's organization.

### Features/Manifests/GenerateBoxQR
- **Endpoint**: `POST /api/manifests/boxes/{boxId}/qr`
- **Logic**: Generate a QR code containing the Box ID/Metadata, upload to Azure, and return the URL.

## 3. Implementation Steps
1. Add `QRCoder` NuGet package.
2. Implement `AzureStorageService` in `Shared/Infrastructure`.
3. Implement `QRCodeService` in `Shared/Infrastructure`.
4. Create `Features/Manifests` slice structure.
5. Implement endpoints and register in `Program.cs`.

## 4. Verification
- Verify QR code generation produces a valid image.
- Verify storage service correctly uploads to the Azure emulator/container.
