import { Button, List } from '@altinn/altinn-components'
import { useTranslation } from 'react-i18next'
import { useIncompleteUploads } from '../api/hooks/useIncompleteUploads'
import { planFileSize } from '../api/uploadPlan'
import { FileTransferCard } from '../components/FileTransfersPages/FileTransferCard'
import { formatFileSize } from '../helpers/fileSizeHelper'
import { useParties, type SelectedParty } from '../parties/PartiesContext'
import { SelectedPartyMessage } from '../parties/SelectedPartyMessage'
import { resumeUploadPath } from './routes'
import './pages.css'

/** Transfers the sender started but whose file never finished uploading, so no recipient has them yet. */
export function DraftFileTransfersPage() {
  const { t } = useTranslation()
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText={t('page.list.drafts_loading')} />
  }

  // A fresh list per actor, instead of unpicking which request belonged to whom.
  return <DraftFileTransfers key={selectedParty.partyUuid} party={selectedParty} />
}

function DraftFileTransfers({ party }: { party: SelectedParty }) {
  const { t } = useTranslation()
  const { uploads, isLoading, isError, hasNextPage, isFetchingNextPage, fetchNextPage } =
    useIncompleteUploads(party)

  return (
    <div className="page">
      <h1 className="page-heading">{t('page.list.drafts_heading')}</h1>
      <p>{t('page.list.drafts_intro')}</p>

      {isError && <p className="empty-state">{t('page.list.drafts_error')}</p>}

      {isLoading && <p className="empty-state">{t('page.list.drafts_loading')}</p>}

      {!isLoading && !isError && uploads.length === 0 && (
        <p className="empty-state">{t('page.list.drafts_empty')}</p>
      )}

      <List className="card-list">
        {uploads.map(({ summary, plan }) => {
          const fileSize = planFileSize(plan)
          return (
            <li key={summary.fileTransferId}>
              <FileTransferCard
                isSender={summary.isSender}
                sender={summary.sender}
                recipients={summary.recipients}
                currentActorName={party.name}
                reference={summary.sendersFileTransferReference || summary.fileTransferId}
                status={{ value: 'requires-attention', label: t('page.status.requires_action') }}
                extendedStatusLabel={t('page.list.drafts_progress', {
                  percent: Math.floor((plan.uploadedBytes / fileSize) * 100),
                  size: formatFileSize(fileSize),
                })}
                to={resumeUploadPath(summary.resourceId, summary.fileTransferId)}
              />
            </li>
          )
        })}
      </List>

      {hasNextPage && (
        <div className="load-more-row">
          <Button
            variant="outline"
            size="lg"
            disabled={isFetchingNextPage}
            onClick={() => void fetchNextPage()}
          >
            {isFetchingNextPage ? t('page.list.loading_more') : t('page.list.load_more')}
          </Button>
        </div>
      )}
    </div>
  )
}
