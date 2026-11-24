import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import {
  fetchServiceOwner,
  initializeServiceOwner,
  provisioningFromOverview,
  type ServiceOwnerProvisioning,
} from '../../api/serviceOwnerProvisioning'
import { localizeApiError, type LocalizedApiError } from '../../i18n/apiErrors'
import { i18n } from '../../i18n/config'

const POLL_INTERVAL_MS = 3000

/** Publish on the gatekeeper resource is missing. Other 403s stay silent. */
function gatekeeperNotice(caught: unknown): LocalizedApiError | null {
  if (!(caught instanceof ApiError) || caught.status !== 403) {
    return null
  }
  const localized = localizeApiError(caught.body)
  return localized.errorCode === 36 ? localized : null
}

/**
 * Loads the selected party's service owner and polls GetServiceOwner while storage deployment runs.
 * `onReady` runs once when deployment finishes, so the resource list can be loaded again.
 */
export function useServiceOwnerProvisioning(
  party: string | undefined,
  organizationName: string,
  onReady: () => void,
) {
  const [provisioning, setProvisioning] = useState<ServiceOwnerProvisioning | null>(null)
  const [allowed, setAllowed] = useState(false)
  const [loaded, setLoaded] = useState(false)
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [accessNotice, setAccessNotice] = useState<LocalizedApiError | null>(null)
  const wasInProgress = useRef(false)
  const partyRef = useRef(party)
  const requestId = useRef(0)
  partyRef.current = party

  const refresh = useCallback(async () => {
    if (!party) {
      return
    }
    const requestedParty = party
    const id = ++requestId.current

    try {
      const next = provisioningFromOverview(await fetchServiceOwner(requestedParty))
      if (partyRef.current !== requestedParty || requestId.current !== id) {
        return
      }
      setProvisioning(next)
      setAllowed(true)
      setLoaded(true)
      setError(null)
      setAccessNotice(null)
      if (next.status === 'InProgress') {
        wasInProgress.current = true
      }
      if (next.status === 'Ready' && wasInProgress.current) {
        wasInProgress.current = false
        onReady()
      }
    } catch (caught: unknown) {
      if (partyRef.current !== requestedParty || requestId.current !== id) {
        return
      }
      setLoaded(true)
      if (caught instanceof ApiError && caught.status === 403) {
        setAllowed(false)
        setProvisioning(null)
        setError(null)
        setAccessNotice(gatekeeperNotice(caught))
        return
      }
      setAccessNotice(null)
      setError(i18n.t('page.services.provision_poll_failed'))
    }
  }, [onReady, party])

  useEffect(() => {
    setProvisioning(null)
    setAllowed(false)
    setLoaded(false)
    setStarting(false)
    setError(null)
    setAccessNotice(null)
    wasInProgress.current = false
    if (!party) {
      return
    }

    let cancelled = false
    const requestedParty = party
    const id = ++requestId.current
    fetchServiceOwner(requestedParty)
      .then((overview) => {
        if (cancelled || requestId.current !== id) {
          return
        }
        const next = provisioningFromOverview(overview)
        setProvisioning(next)
        setAllowed(true)
        setLoaded(true)
        setAccessNotice(null)
        if (next.status === 'InProgress') {
          wasInProgress.current = true
        }
      })
      .catch((caught: unknown) => {
        if (cancelled || requestId.current !== id) {
          return
        }
        setLoaded(true)
        if (caught instanceof ApiError && caught.status === 403) {
          setAllowed(false)
          setProvisioning(null)
          setAccessNotice(gatekeeperNotice(caught))
          return
        }
        setAccessNotice(null)
        setError(i18n.t('page.services.provision_poll_failed'))
      })

    return () => {
      cancelled = true
    }
  }, [party])

  useEffect(() => {
    if (!party || provisioning != null || !error) {
      return
    }

    const timer = window.setInterval(() => {
      void refresh()
    }, POLL_INTERVAL_MS)
    return () => window.clearInterval(timer)
  }, [error, party, provisioning, refresh])

  useEffect(() => {
    if (!party || provisioning?.status !== 'InProgress') {
      return
    }

    const timer = window.setInterval(() => {
      void refresh()
    }, POLL_INTERVAL_MS)
    return () => window.clearInterval(timer)
  }, [party, provisioning?.status, refresh])

  const start = useCallback(() => {
    if (!party || starting) {
      return
    }
    const requestedParty = party

    setStarting(true)
    setError(null)
    initializeServiceOwner(requestedParty, organizationName)
      .catch((caught: unknown) => {
        if (caught instanceof ApiError && caught.status === 409) {
          return
        }
        throw caught
      })
      .then(() => {
        if (partyRef.current !== requestedParty) {
          return
        }
        wasInProgress.current = true
        setProvisioning({
          status: 'InProgress',
          percent: 40,
          messageKey: 'page.services.provision_phase_waiting',
        })
        return refresh()
      })
      .catch((caught: unknown) => {
        if (partyRef.current !== requestedParty) {
          return
        }
        const body = caught instanceof ApiError ? caught.body : undefined
        setError(localizeApiError(body).message || i18n.t('page.services.provision_start_failed'))
      })
      .finally(() => {
        if (partyRef.current !== requestedParty) {
          return
        }
        setStarting(false)
      })
  }, [organizationName, party, refresh, starting])

  return { provisioning, allowed, loaded, starting, error, accessNotice, start, retry: refresh }
}
