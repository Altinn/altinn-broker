import os
import tempfile
from pathlib import Path

WRITE_CHUNK_SIZE = 8 * 1024 * 1024


def create_upload_file(byte_length: int) -> str:
    file_path = Path(tempfile.gettempdir()) / f"altinn-broker-tus-upload-{os.getpid()}.bin"

    remaining = byte_length
    with file_path.open("wb") as stream:
        while remaining > 0:
            chunk_size = min(WRITE_CHUNK_SIZE, remaining)
            stream.write(os.urandom(chunk_size))
            remaining -= chunk_size

    return str(file_path)
