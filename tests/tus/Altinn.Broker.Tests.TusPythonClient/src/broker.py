import requests

EMPTY_FILE_TRANSFER_ID = "00000000-0000-0000-0000-000000000000"


def _format_party_id(org_number: str) -> str:
    return org_number if ":" in org_number else f"0192:{org_number}"


def _build_initialize_payload(org_number: str, resource_id: str) -> dict:
    return {
        "resourceId": resource_id,
        "fileName": "input.txt",
        "propertyList": {},
        "recipients": ["0192:310880442"],
        "sender": _format_party_id(org_number),
        "sendersFileTransferReference": "test-data",
        "disableVirusScan": True,
    }


def initialize_file_transfer(
    base_url: str, token: str, org_number: str, resource_id: str
) -> str:
    response = requests.post(
        f"{base_url.rstrip('/')}/broker/api/v1/filetransfer",
        headers={
            "Authorization": f"Bearer {token}",
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
        json=_build_initialize_payload(org_number, resource_id),
        timeout=60,
    )
    if not response.ok:
        raise RuntimeError(
            f"Initialize file transfer failed with {response.status_code}: {response.text}"
        )

    payload = response.json()
    file_transfer_id = payload.get("fileTransferId")
    if not file_transfer_id or file_transfer_id == EMPTY_FILE_TRANSFER_ID:
        raise RuntimeError(
            f"Initialize response did not include fileTransferId. Body: {response.text}"
        )
    return file_transfer_id


def verify_published(base_url: str, token: str, file_transfer_id: str) -> None:
    response = requests.get(
        f"{base_url.rstrip('/')}/broker/api/v1/filetransfer/{file_transfer_id}",
        headers={
            "Authorization": f"Bearer {token}",
            "Accept": "application/json",
        },
        timeout=60,
    )
    if not response.ok:
        raise RuntimeError(
            f"Overview request failed with {response.status_code}: {response.text}"
        )

    overview = response.json()
    if overview.get("fileTransferStatus") != "Published":
        raise RuntimeError(
            f"Expected Published status, got {overview.get('fileTransferStatus')}"
        )

    print(f"Verified file transfer {file_transfer_id} is Published.")
