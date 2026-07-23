import { toUrn } from './commonUtils.js';

export const TEST_TAG_A3 = 'useCaseTestsA3';
const sender = __ENV.sender;

export function buildInitializeFileTransferPayload(recipientOrgNo) {
    const recipient = toUrn(recipientOrgNo);
    const nowRef = `usecase-broker-${Date.now()}`;

    return {
        resourceId: "bruksmonster-broker",
        fileName: 'usecase-broker-test-file.txt',
        sendersFileTransferReference: nowRef,
        sender: `0192:${sender}`,
        recipients: [recipient],
        propertyList: {
            testTag: TEST_TAG_A3,
            useCase: 'Use case tests',
            description: 'Test file transfer initialization for use case tests'
        },
        disableVirusScan: true
    };
}