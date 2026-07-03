const TEST_RESOURCE = 'altinn-broker-test-resource-2';

export async function getAccessToken(username, password, orgNumber) {
  const url =
    `https://altinn-testtools-token-generator.azurewebsites.net/api/GetEnterpriseToken` +
    `?env=tt02&scopes=altinn:broker.write altinn:serviceowner&org=ttd&orgNo=${orgNumber}`;

  const credentials = Buffer.from(`${username}:${password}`).toString('base64');
  const response = await fetch(url, {
    headers: { Authorization: `Basic ${credentials}` },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Token request failed with ${response.status}: ${body}`);
  }

  return response.text();
}

export async function configureResource(baseUrl, token, uploadSize) {
  const response = await fetch(`${baseUrl}/broker/api/v1/resource/${TEST_RESOURCE}`, {
    method: 'PUT',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ maxFileTransferSize: uploadSize + 1 }),
  });

  if (!response.ok) {
    const body = await response.text();
    console.warn(`Configure resource returned ${response.status}: ${body}`);
  }
}

export async function initializeFileTransfer(baseUrl, token) {
  const response = await fetch(`${baseUrl}/broker/api/v1/filetransfer`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
      Accept: 'application/json',
    },
    body: JSON.stringify({
      resourceId: TEST_RESOURCE,
      fileName: 'input.txt',
      propertyList: {},
      recipients: ['0192:986252932'],
      sender: '0192:991825827',
      sendersFileTransferReference: 'tus-js-client-test',
      disableVirusScan: true,
    }),
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Initialize file transfer failed with ${response.status}: ${body}`);
  }

  const payload = await response.json();
  if (!payload.fileTransferId) {
    throw new Error('Initialize response did not include fileTransferId.');
  }

  return payload.fileTransferId;
}

export async function getFileTransferOverview(baseUrl, token, fileTransferId) {
  const response = await fetch(`${baseUrl}/broker/api/v1/filetransfer/${fileTransferId}`, {
    headers: {
      Authorization: `Bearer ${token}`,
      Accept: 'application/json',
    },
  });

  if (!response.ok) {
    const body = await response.text();
    throw new Error(`Overview request failed with ${response.status}: ${body}`);
  }

  return response.json();
}
