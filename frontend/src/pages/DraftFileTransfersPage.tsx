import { Button, List } from '@altinn/altinn-components'
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
  const { selectedParty } = useParties()

  if (!selectedParty) {
    return <SelectedPartyMessage loadingText="Laster utkast …" />
  }

  // A fresh list per actor, instead of unpicking which request belonged to whom.
  return <DraftFileTransfers key={selectedParty.partyUuid} party={selectedParty} />
}

function DraftFileTransfers({ party }: { party: SelectedParty }) {
  const { uploads, resources, isLoading, isError, hasNextPage, isFetchingNextPage, fetchNextPage } =
    useIncompleteUploads(party)

  const resourceName = (resourceId: string) =>
    resources.find((resource) => resource.resourceId === resourceId)?.name ?? resourceId

  return (
    <div className="page">
      <h1 className="page-heading">Utkast</h1>
      <p>
        Formidlinger der filen ikke er ferdig lastet opp, og som derfor ikke er sendt til mottakerne ennå. Velg en
        for å fortsette opplastingen der den slapp. Opplastinger som ikke fullføres innen et døgn, avbrytes.
      </p>

      {isError && <p className="empty-state">Klarte ikke å hente utkastene.</p>}

      {isLoading && <p className="empty-state">Laster utkast …</p>}

      {!isLoading && !isError && uploads.length === 0 && <p className="empty-state">Ingen utkast funnet.</p>}

      <List className="card-list">
        {uploads.map(({ summary, plan }) => {
          const fileSize = planFileSize(plan)
          return (
            <li key={summary.fileTransferId}>
              <FileTransferCard
                resourceName={resourceName(summary.resourceId)}
                isSender={summary.isSender}
                sender={summary.sender}
                recipients={summary.recipients}
                currentActorName={party.name}
                reference={summary.sendersFileTransferReference || summary.fileTransferId}
                status={{ value: 'requires-attention', label: 'Krever handling' }}
                extendedStatusLabel={`${Math.floor((plan.uploadedBytes / fileSize) * 100)} % av ${formatFileSize(fileSize)} lastet opp`}
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
            {isFetchingNextPage ? 'Henter …' : 'Hent flere'}
          </Button>
        </div>
      )}
    </div>
  )
}
