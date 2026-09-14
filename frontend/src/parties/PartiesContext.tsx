import type { AuthorizedParty } from '@altinn/altinn-components'
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { fetchAuthorizedParties, type AuthorizedPartyDto } from '../api/parties'
import { canBeRepresented, flattenParties, toAuthorizedParty } from './mapAuthorizedParty'

const SELECTED_PARTY_STORAGE_KEY = 'brokerbox.selectedPartyUuid'

/** The party the user currently acts on behalf of. */
export type SelectedParty = {
  partyUuid: string
  name: string
  organizationNumber: string
}

type PartiesContextValue = {
  status: 'loading' | 'loaded' | 'failed'
  /** The party hierarchy, in the shape the account selector expects. */
  parties: AuthorizedParty[]
  /** Null while loading, on failure, and when the user represents no organizations. */
  selectedParty: SelectedParty | null
  selectParty: (partyUuid: string) => void
}

const PartiesContext = createContext<PartiesContextValue | null>(null)

function readStoredPartyUuid(): string | null {
  try {
    return window.localStorage.getItem(SELECTED_PARTY_STORAGE_KEY)
  } catch {
    return null
  }
}

function storePartyUuid(partyUuid: string) {
  try {
    window.localStorage.setItem(SELECTED_PARTY_STORAGE_KEY, partyUuid)
  } catch {
    // Private mode or blocked storage: the selection simply does not survive a reload.
  }
}

export function PartiesProvider({ children }: { children: ReactNode }) {
  const [status, setStatus] = useState<PartiesContextValue['status']>('loading')
  const [authorizedParties, setAuthorizedParties] = useState<AuthorizedPartyDto[]>([])
  const [selectedPartyUuid, setSelectedPartyUuid] = useState<string | null>(readStoredPartyUuid)

  useEffect(() => {
    let active = true

    fetchAuthorizedParties()
      .then((parties) => {
        if (!active) {
          return
        }
        setAuthorizedParties(parties)
        setStatus('loaded')
      })
      .catch(() => {
        if (active) {
          setStatus('failed')
        }
      })

    return () => {
      active = false
    }
  }, [])

  // TODO(#1003): persons are left out because the Broker API identifies parties by organization
  // number. Revisit if BrokerBox should let users act as themselves.
  const parties = useMemo(
    () =>
      authorizedParties
        .filter((party) => party.type === 'Organization')
        .map(toAuthorizedParty),
    [authorizedParties],
  )

  const representableParties = useMemo(
    () => flattenParties(authorizedParties).filter(canBeRepresented),
    [authorizedParties],
  )

  const selectedParty = useMemo<SelectedParty | null>(() => {
    const party =
      representableParties.find((candidate) => candidate.partyUuid === selectedPartyUuid) ??
      representableParties[0]

    if (!party?.organizationNumber) {
      return null
    }

    return {
      partyUuid: party.partyUuid,
      name: party.name ?? '',
      organizationNumber: party.organizationNumber,
    }
  }, [representableParties, selectedPartyUuid])

  const selectParty = useCallback((partyUuid: string) => {
    setSelectedPartyUuid(partyUuid)
    storePartyUuid(partyUuid)
  }, [])

  const value = useMemo<PartiesContextValue>(
    () => ({ status, parties, selectedParty, selectParty }),
    [status, parties, selectedParty, selectParty],
  )

  return <PartiesContext.Provider value={value}>{children}</PartiesContext.Provider>
}

export function useParties(): PartiesContextValue {
  const ctx = useContext(PartiesContext)
  if (!ctx) {
    throw new Error('useParties must be used within PartiesProvider')
  }
  return ctx
}
