import json
import os
import time
from typing import NamedTuple
from urllib.parse import urlparse

import jwt
import requests

BROKER_WRITE_SCOPE = "altinn:broker.write"


class AuthOptions(NamedTuple):
    base_url: str
    client_id: str
    client_kid: str
    client_private_key_pem: str
    org_number: str
    maskinporten_token_url: str | None


def _read_env(name: str, fallback: str | None) -> str | None:
    value = os.environ.get(name)
    if value is None or value == "":
        return fallback
    return value


def _require_env(name: str) -> str:
    value = os.environ.get(name)
    if not value:
        raise RuntimeError(f"Missing required environment variable: {name}")
    return value


def _read_private_key_pem() -> str:
    file_path = os.environ.get("CLIENT_SECRET_FILE") or os.environ.get("CLIENT_PEM_FILE")
    if file_path:
        with open(file_path, encoding="utf-8") as pem_file:
            return pem_file.read()

    pem = os.environ.get("CLIENT_SECRET") or os.environ.get("CLIENT_PEM")
    if not pem:
        raise RuntimeError(
            "Missing required environment variable: CLIENT_SECRET (Maskinporten private key PEM). "
            "CLIENT_PEM is also accepted. Use CLIENT_SECRET_FILE for a PEM file path."
        )

    return pem.replace("\\n", "\n")


def _is_production_platform(base_url: str) -> bool:
    lowered = base_url.lower()
    return "platform.altinn.no" in lowered and "tt02" not in lowered


def _resolve_maskinporten_token_url(base_url: str, override: str | None) -> str:
    if override:
        return override

    return (
        "https://maskinporten.no/token"
        if _is_production_platform(base_url)
        else "https://test.maskinporten.no/token"
    )


def _get_maskinporten_audience(token_url: str) -> str:
    parsed = urlparse(token_url)
    return f"{parsed.scheme}://{parsed.netloc}/"


def _create_maskinporten_client_assertion(
    *,
    client_id: str,
    client_kid: str,
    private_key_pem: str,
    org_number: str,
    token_url: str,
) -> str:
    now = int(time.time())
    payload = {
        "aud": _get_maskinporten_audience(token_url),
        "scope": BROKER_WRITE_SCOPE,
        "iss": client_id,
        "iat": now,
        "exp": now + 120,
        "authorization_details": [
            {
                "type": "urn:altinn:systemuser",
                "systemuser_org": {
                    "authority": "iso6523-actorid-upis",
                    "ID": org_number,
                },
            }
        ],
    }
    return jwt.encode(
        payload,
        private_key_pem,
        algorithm="RS256",
        headers={"kid": client_kid},
    )


def _parse_altinn_exchange_token(body: str) -> str:
    trimmed = body.strip()
    if trimmed.startswith("eyJ"):
        return trimmed

    if trimmed.startswith('"'):
        parsed = json.loads(trimmed)
        if isinstance(parsed, str):
            return parsed
        raise RuntimeError("Altinn token exchange returned an empty string token.")

    parsed = json.loads(trimmed)
    if isinstance(parsed, str):
        return parsed

    access_token = parsed.get("access_token")
    if access_token:
        return access_token

    raise RuntimeError(f"Unexpected Altinn token exchange response: {body}")


def read_auth_options_from_environment() -> AuthOptions:
    return AuthOptions(
        base_url=_read_env("BASE_URL", "https://platform.tt02.altinn.no"),
        client_id=_require_env("CLIENT_ID"),
        client_kid=_require_env("CLIENT_KID"),
        client_private_key_pem=_read_private_key_pem(),
        org_number=_require_env("ORG_NO"),
        maskinporten_token_url=os.environ.get("MASKINPORTEN_TOKEN_URL"),
    )


def _request_maskinporten_token(options: AuthOptions) -> str:
    token_url = _resolve_maskinporten_token_url(options.base_url, options.maskinporten_token_url)
    assertion = _create_maskinporten_client_assertion(
        client_id=options.client_id,
        client_kid=options.client_kid,
        private_key_pem=options.client_private_key_pem,
        org_number=options.org_number,
        token_url=token_url,
    )

    for attempt in range(2):
        response = requests.post(
            token_url,
            headers={
                "Accept": "application/json",
                "Content-Type": "application/x-www-form-urlencoded",
            },
            data={
                "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
                "assertion": assertion,
            },
            timeout=60,
        )

        try:
            payload = response.json()
        except ValueError:
            payload = {}

        if response.status_code != 503 or attempt == 1:
            access_token = payload.get("access_token")
            if not response.ok or not access_token:
                raise RuntimeError(
                    "Maskinporten token request failed. "
                    f"Status={response.status_code}. "
                    f"Error={payload.get('error')}. "
                    f"Description={payload.get('error_description')}"
                )
            return access_token

        time.sleep(1)

    raise RuntimeError("Maskinporten token request failed after retry.")


def _exchange_maskinporten_token(base_url: str, maskinporten_token: str) -> str:
    exchange_url = f"{base_url.rstrip('/')}/authentication/api/v1/exchange/maskinporten"
    response = requests.get(
        exchange_url,
        headers={
            "Authorization": f"Bearer {maskinporten_token}",
            "Accept": "application/json",
        },
        timeout=60,
    )
    body = response.text
    if not response.ok or not body.strip():
        raise RuntimeError(
            f"Altinn token exchange failed. Status={response.status_code}. Body={body}"
        )

    return _parse_altinn_exchange_token(body)


def exchange_altinn_token(options: AuthOptions) -> str:
    maskinporten_token = _request_maskinporten_token(options)
    return _exchange_maskinporten_token(options.base_url, maskinporten_token)
