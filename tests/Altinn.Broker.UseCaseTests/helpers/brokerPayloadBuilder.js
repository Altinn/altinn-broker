import { toUrn } from './commonUtils.js';

export const TEST_TAG_A3 = 'useCaseTestsA3';
export const TEST_TAG_LEGACY = 'useCaseTestsLegacy';

export function buildInitializeFileTransferPayload(recipientOrgNo) {
    const recipient = toUrn(recipientOrgNo);
    const nowRef = `usecase-broker-${Date.now()}`;

    return {
        resourceId: "bruksmonster-broker",
        fileName: 'usecase-broker-test-file.txt',
        sendersFileTransferReference: nowRef,
        sender: isProduction ? `0192:${prodSender}` : '0192:313896013',
        recipients: [recipient],
        propertyList: {
            testTag: TEST_TAG_A3,
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
        sender: isProduction ? `0192:${prodSender}` : '0192:313896013',
        recipients: [recipient],
        propertyList: {
            testTag: TEST_TAG_LEGACY,
            useCase: 'Use case tests',
            description: 'Test file transfer initialization for legacy use case tests'
        },
        disableVirusScan: false
    };
}