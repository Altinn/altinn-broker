import os
import sys
import time
from pathlib import Path

from tusclient import client as tus

from altinn_auth import exchange_altinn_token, read_auth_options_from_environment
from broker import initialize_file_transfer, verify_published
from generate_file import create_upload_file

DEFAULT_CHUNK_SIZE_MB = 8
DEFAULT_PARALLEL_UPLOADS = 4
DEFAULT_UPLOAD_MIB = 64


def read_env(name: str, fallback: str | None) -> str | None:
    value = os.environ.get(name)
    if value is None or value == "":
        return fallback
    return value


def require_env(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def format_gib(bytes_value: int | float) -> str:
    return f"{bytes_value / (1024 * 1024 * 1024):.2f}"


def format_mib(bytes_value: int | float) -> str:
    return f"{bytes_value / (1024 * 1024):.2f}"


def log_upload_size(upload_bytes: int) -> None:
    print(f"Upload size: {format_gib(upload_bytes)} GiB ({format_mib(upload_bytes)} MiB)")


def upload_with_tus_py_client(
    *,
    base_url: str,
    token: str,
    file_transfer_id: str,
    file_path: str,
    chunk_size: int,
    parallel_uploads: int,
) -> None:
    file_size = Path(file_path).stat().st_size
    endpoint = (
        f"{base_url.rstrip('/')}/broker/api/v1/filetransfer/upload/tus/{file_transfer_id}"
    )
    started_at = time.monotonic()
    upload_label = (
        "TUS concatenation upload" if parallel_uploads > 1 else "TUS upload"
    )

    tus_client = tus.TusClient(
        endpoint,
        headers={"Authorization": f"Bearer {token}"},
    )

    def on_progress(bytes_uploaded: int, bytes_total: int | None) -> None:
        total = bytes_total or file_size
        elapsed_seconds = max(time.monotonic() - started_at, 0.001)
        mib_per_second = bytes_uploaded / elapsed_seconds / (1024 * 1024)
        percentage = (bytes_uploaded / total) * 100
        sys.stdout.write(
            f"\rProgress: {percentage:.1f}% "
            f"({format_gib(bytes_uploaded)} / {format_gib(total)} GiB, "
            f"{mib_per_second:.2f} MiB/s)"
        )
        sys.stdout.flush()

    uploader = tus_client.uploader(
        file_path=file_path,
        chunk_size=chunk_size,
        parallel_uploads=parallel_uploads,
        retries=5,
        retry_delays=[1000, 2000, 4000, 8000, 16000],
        metadata={
            "filename": "input.txt",
            "filetype": "application/octet-stream",
        },
        on_progress=on_progress,
    )
    uploader.upload()

    print()
    print(f"TUS upload finished: {uploader.url}")

    elapsed_seconds = max(time.monotonic() - started_at, 0.001)
    average_speed_mib = file_size / elapsed_seconds / (1024 * 1024)
    print(
        f"{upload_label} completed in {elapsed_seconds:.1f}s "
        f"(avg: {average_speed_mib:.2f} MiB/s)"
    )


def resolve_upload_file(upload_file_path: str) -> tuple[Path, int, bool]:
    file_path = Path(upload_file_path).expanduser().resolve()
    if not file_path.is_file():
        raise RuntimeError(
            f"UPLOAD_FILE_PATH does not exist or is not a file: {upload_file_path}"
        )
    return file_path, file_path.stat().st_size, False


def create_generated_upload_file(upload_bytes: int) -> tuple[Path, int, bool]:
    return Path(create_upload_file(upload_bytes)), upload_bytes, True


def main() -> None:
    auth_options = read_auth_options_from_environment()
    base_url = auth_options.base_url
    resource_id = require_env("RESOURCE_ID")
    chunk_size_mb = int(read_env("CHUNK_SIZE_MB", str(DEFAULT_CHUNK_SIZE_MB)))
    parallel_uploads = int(
        read_env("TUS_PARALLEL_PARTIAL_UPLOADS", str(DEFAULT_PARALLEL_UPLOADS))
    )
    upload_file_path = read_env("UPLOAD_FILE_PATH", None)
    chunk_size = chunk_size_mb * 1024 * 1024

    print(f"BASE_URL: {base_url}")
    print(f"RESOURCE_ID: {resource_id}")
    print(f"ORG_NO: {auth_options.org_number}")
    print(f"CHUNK_SIZE_MB: {chunk_size_mb}")
    print(f"TUS_PARALLEL_PARTIAL_UPLOADS: {parallel_uploads}")

    token = exchange_altinn_token(auth_options)
    file_transfer_id = initialize_file_transfer(
        base_url, token, auth_options.org_number, resource_id
    )
    print(f"File transfer id: {file_transfer_id}")

    if upload_file_path:
        file_path, upload_bytes, cleanup_file = resolve_upload_file(upload_file_path)
    else:
        gigabytes = os.environ.get("GIGABYTES_TO_UPLOAD")
        upload_bytes = (
            int(gigabytes) * 1024 * 1024 * 1024
            if gigabytes
            else DEFAULT_UPLOAD_MIB * 1024 * 1024
        )
        file_path, upload_bytes, cleanup_file = create_generated_upload_file(upload_bytes)

    log_upload_size(upload_bytes)
    if upload_file_path:
        print(f"UPLOAD_FILE_PATH: {file_path}")
    else:
        print(f"Temporary upload file: {file_path}")

    try:
        upload_with_tus_py_client(
            base_url=base_url,
            token=token,
            file_transfer_id=file_transfer_id,
            file_path=str(file_path),
            chunk_size=chunk_size,
            parallel_uploads=parallel_uploads,
        )

        verify_published(base_url, token, file_transfer_id)
    finally:
        if cleanup_file:
            file_path.unlink(missing_ok=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print(error, file=sys.stderr)
        sys.exit(1)
