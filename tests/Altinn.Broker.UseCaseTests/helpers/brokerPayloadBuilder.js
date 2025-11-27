import { toUrn } from './commonUtils.js';

export function buildInitializeFileTransferPayload(resourceId, recipientOrgNo) {
    const recipient = toUrn(recipientOrgNo);
    const nowRef = `usecase-broker-${Date.now()}`;

    return {
        resourceId: "bruksmonster-broker",
        fileName: 'usecase-broker-test-file.txt',
        sendersFileTransferReference: nowRef,
        sender: '0192:313896013',
        recipients: [recipient],
        propertyList: {
            useCase: 'TT02',
            description: 'Test file transfer initialization for use case TT02'
        },
        disableVirusScan: false
    };
}