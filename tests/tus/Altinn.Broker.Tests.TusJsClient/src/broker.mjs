function formatPartyId(orgNumber) {
  return orgNumber.includes(':') ? orgNumber : `0192:${orgNumber}`;
}

function buildInitializePayload(orgNumber, resourceId) {
  return {
    resourceId,
    fileName: 'input.txt',
    propertyList: {},
    recipients: ['0192:310880442'],
    sender: formatPartyId(orgNumber),
    sendersFileTransferReference: 'test-data',
    disableVirusScan: true,
  };
}

export async function initializeFileTransfer(baseUrl, token, orgNumber, resourceId) {
  const response = await fetch(`${baseUrl.replace(/\/$/, '')}/broker/api/v1/filetransfer`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify(buildInitializePayload(orgNumber, resourceId)),
  });

  const body = await response.text();
  if (!response.ok) {
    throw new Error(`Initialize file transfer failed with ${response.status}: ${body}`);
  }

  const payload = JSON.parse(body);
  if (!payload.fileTransferId || payload.fileTransferId === '00000000-0000-0000-0000-000000000000') {
    throw new Error(`Initialize response did not include fileTransferId. Body: ${body}`);
  }

  return payload.fileTransferId;
}

export async function verifyPublished(baseUrl, token, fileTransferId) {
  const response = await fetch(
    `${baseUrl.replace(/\/$/, '')}/broker/api/v1/filetransfer/${fileTransferId}`,
    {
      headers: {
        Authorization: `Bearer ${token}`,
        Accept: 'application/json',
      },
    },
  );

  const body = await response.text();
  if (!response.ok) {
    throw new Error(`Overview request failed with ${response.status}: ${body}`);
  }

  const overview = JSON.parse(body);
  if (overview.fileTransferStatus !== 'Published') {
    throw new Error(`Expected Published status, got ${overview.fileTransferStatus}`);
  }

  console.log(`Verified file transfer ${fileTransferId} is Published.`);
}
