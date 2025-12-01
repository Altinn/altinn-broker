import { toUrn } from './commonUtils.js';

export function buildInitializeFileTransferPayload(recipientOrgNo) {
    const recipient = toUrn(recipientOrgNo);
    const nowRef = `usecase-broker-${Date.now()}`;

    return {
        resourceId: "bruksmonster-broker",
        fileName: 'usecase-broker-test-file.txt',
        sendersFileTransferReference: nowRef,
        sender: '0192:313896013',
        recipients: [recipient],
        propertyList: {
            useCase: 'Use case tests',
            description: 'Test file transfer initialization for use case tests'
        },
        disableVirusScan: false
    };
}

export function buildLegacyInitializeFileTransferPayload(recipientOrgNo) {
    const recipient = "0192:" + recipientOrgNo;
    const nowRef = `legacy-usecase-broker-${Date.now()}`;
    return {
        resourceId: "bruksmonster-broker",
        fileName: 'usecase-broker-test-file.txt',
        sendersFileTransferReference: nowRef,
        sender: '0192:313896013',
        recipients: [recipient],
        propertyList: {
            useCase: 'Use case tests',
            description: 'Test file transfer initialization for legacy use case tests'
        },
        disableVirusScan: false
    };
}