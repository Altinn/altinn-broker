import { getMaintenanceMaskinportenToken } from './maskinportenTokenService.js';
import { check } from 'k6';
import http from 'k6/http';

const baseUrl = __ENV.base_url;

export async function cleanupUseCaseTestData() {
	const token = await getMaintenanceMaskinportenToken();
	check(token, { 'Maintenance Maskinporten token obtained for cleanup': t => typeof t === 'string' && t.length > 0 });

	const headers = {
		Authorization: `Bearer ${token}`
	};

	const res = http.post(`${baseUrl}/broker/api/v1/maintenance/cleanup-usecasetests`, null, { headers });
	check(res, { 'Cleanup use case test data status 200': r => r.status === 200 });

	if (res.status === 200) {
        const body = res.json();
        console.log(`Cleanup summary: resourceId=${body.resourceId}, fileTransfersFound=${body.fileTransfersFound}, deleteFileTransfersJobId=${body.deleteFileTransfersJobId}`);
	} else {
		console.error(`Cleanup failed. Status: ${res.status}. Body: ${res.body}`);
	}
}