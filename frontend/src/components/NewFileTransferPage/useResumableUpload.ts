import { useEffect, useState } from 'react'
import { getFileTransferDetails, type FileTransferDetails } from '../../api/fileTransferDetail'
import { getUploadPlan, planFileSize, type StoredUploadPlan } from '../../api/uploadPlan'
import { toOrgNumber } from '../../helpers/orgIdentifierHelper'
import type { SelectedParty } from '../../parties/PartiesContext'
import type { NewFileTransferValues } from './formFields'

// Loads a transfer whose upload stopped, so the form can continue it: the plan for the rest of the
// file, and the form as it was sent, read back from the transfer since this may be another browser.

export type ResumableUpload = {
  fileTransferId: string
  plan: StoredUploadPlan
  /** The file the upload started with, which the user has to pick again. */
  fileName: string
  fileSize: number
  /** The form as the transfer was sent. Everything but the file, which the user picks again. */
  values: Omit<NewFileTransferValues, 'file'>
}

export type ResumableUploadState =
  | { status: 'none' }
  | { status: 'loading' }
  | { status: 'failed' }
  /** The transfer has no upload this sender can continue here. */
  | { status: 'missing' }
  | { status: 'ready'; upload: ResumableUpload }

export function useResumableUpload(
  fileTransferId: string | null,
  resourceId: string,
  party: SelectedParty | null,
): ResumableUploadState {
  const [state, setState] = useState<ResumableUploadState>(
    fileTransferId ? { status: 'loading' } : { status: 'none' },
  )

  useEffect(() => {
    if (!fileTransferId || !party) {
      return
    }
    let current = true
    Promise.all([getFileTransferDetails(fileTransferId, party), getUploadPlan(fileTransferId)])
      .then(([details, plan]) => {
        if (!current) {
          return
        }
        if (!plan || !details.isSender || details.resourceId !== resourceId) {
          setState({ status: 'missing' })
          return
        }
        setState({
          status: 'ready',
          upload: {
            fileTransferId,
            plan,
            fileName: details.fileName ?? '',
            fileSize: planFileSize(plan),
            values: toFormValues(details),
          },
        })
      })
      .catch((error) => {
        console.error('Could not load the upload to continue', error)
        if (current) {
          setState({ status: 'failed' })
        }
      })
    return () => {
      current = false
    }
  }, [fileTransferId, party, resourceId])

  return fileTransferId ? state : { status: 'none' }
}

function toFormValues(details: FileTransferDetails): Omit<NewFileTransferValues, 'file'> {
  const checksum = details.checksum?.trim() ?? ''
  const notification = details.notification ?? { enabled: false, sendReminder: false, customRecipients: [] }
  return {
    reference: details.sendersFileTransferReference ?? '',
    recipients: (details.recipients ?? [])
      .map((recipient) => toOrgNumber(recipient.recipient))
      .filter((organizationNumber): organizationNumber is string => organizationNumber !== null),
    metadata: Object.entries(details.propertyList ?? {}).map(([key, value]) => ({
      id: crypto.randomUUID(),
      key,
      value,
    })),
    checksumEnabled: checksum !== '',
    checksumAlgorithm: details.checksumAlgorithm ?? 'SHA256',
    checksum,
    virusScan: details.useVirusScan ?? true,
    notifications: notification.enabled,
    sendReminder: notification.sendReminder,
    customRecipientsEnabled: notification.customRecipients.length > 0,
    customRecipients: notification.customRecipients.map((customRecipient) => ({
      id: crypto.randomUUID(),
      identifier: customRecipient.identifier,
      relatedOrganizationNumber: customRecipient.relatedOrganizationNumber,
    })),
  }
}
