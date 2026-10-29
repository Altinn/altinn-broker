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
import { useAuth } from '../auth/AuthContext'
import { claimValue } from '../auth/types'
import { canBeRepresented, flattenParties, toAuthorizedParty } from './mapAuthorizedParty'

const SELECTED_PARTY_STORAGE_KEY = 'brokerbox.selectedPartyUuid'

/** Placeholder uuid for the user's own account, which is not part of the organization list. */
const SELF_ACCOUNT_UUID = 'self'

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
  /** The user's own account. The account selector renders nothing without it. */
  selfPartyUuid: string | undefined
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
  const { user } = useAuth()
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

  const allParties = useMemo(() => flattenParties(authorizedParties), [authorizedParties])

  /**
   * The account selector renders nothing without an account for the user themselves, and the
   * list holds organizations only. The user's own account is therefore built from their token
   * claims. It is shown, never selected.
   */
  const selfAccount = useMemo<AuthorizedParty | null>(() => {
    if (!user) {
      return null
    }
    return {
      partyUuid: SELF_ACCOUNT_UUID,
      name: claimValue(user, 'name') ?? 'Deg',
      partyId: claimValue(user, 'urn:altinn:partyid') ?? '',
      type: 'Person',
      isDeleted: false,
      onlyHierarchyElementWithNoAccess: false,
      authorizedResources: [],
      authorizedRoles: [],
    }
  }, [user])

  const parties = useMemo(() => {
    const organizations = authorizedParties
      .filter((party) => party.type === 'Organization')
      .map(toAuthorizedParty)
    return selfAccount ? [selfAccount, ...organizations] : organizations
  }, [authorizedParties, selfAccount])

  // TODO(#1003): only organizations, because the Broker API identifies parties by organization
  // number. Revisit if BrokerBox should let users act as themselves.
  const representableParties = useMemo(() => allParties.filter(canBeRepresented), [allParties])

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

  const selectParty = useCallback(
    (partyUuid: string) => {
      if (!representableParties.some((party) => party.partyUuid === partyUuid)) {
        return
      }
      setSelectedPartyUuid(partyUuid)
      storePartyUuid(partyUuid)
    },
    [representableParties],
  )

  const value = useMemo<PartiesContextValue>(
    () => ({ status, parties, selfPartyUuid: selfAccount?.partyUuid, selectedParty, selectParty }),
    [status, parties, selfAccount, selectedParty, selectParty],
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
