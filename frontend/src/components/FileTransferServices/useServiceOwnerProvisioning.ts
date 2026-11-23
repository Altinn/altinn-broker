import { useCallback, useEffect, useRef, useState } from 'react'
import { ApiError } from '../../api/client'
import {
  fetchServiceOwner,
  initializeServiceOwner,
  provisioningFromOverview,
  type ServiceOwnerProvisioning,
} from '../../api/serviceOwnerProvisioning'
import { localizeApiError } from '../../i18n/apiErrors'
import { i18n } from '../../i18n/config'

const POLL_INTERVAL_MS = 3000

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
  const wasInProgress = useRef(false)

  const refresh = useCallback(async () => {
    if (!party) {
      return
    }

    try {
      const next = provisioningFromOverview(await fetchServiceOwner(party))
      setProvisioning(next)
      setAllowed(true)
      setLoaded(true)
      setError(null)
      if (next.status === 'InProgress') {
        wasInProgress.current = true
      }
      if (next.status === 'Ready' && wasInProgress.current) {
        wasInProgress.current = false
        onReady()
      }
    } catch (caught: unknown) {
      setLoaded(true)
      if (caught instanceof ApiError && caught.status === 403) {
        setAllowed(false)
        setProvisioning(null)
        return
      }
      setError(i18n.t('page.services.provision_poll_failed'))
    }
  }, [onReady, party])

  useEffect(() => {
    setProvisioning(null)
    setAllowed(false)
    setLoaded(false)
    setStarting(false)
    setError(null)
    wasInProgress.current = false
    if (!party) {
      return
    }

    let cancelled = false
    fetchServiceOwner(party)
      .then((overview) => {
        if (cancelled) {
          return
        }
        const next = provisioningFromOverview(overview)
        setProvisioning(next)
        setAllowed(true)
        setLoaded(true)
        if (next.status === 'InProgress') {
          wasInProgress.current = true
        }
      })
      .catch((caught: unknown) => {
        if (cancelled) {
          return
        }
        setLoaded(true)
        if (caught instanceof ApiError && caught.status === 403) {
          setAllowed(false)
          setProvisioning(null)
          return
        }
        setError(i18n.t('page.services.provision_poll_failed'))
      })

    return () => {
      cancelled = true
    }
  }, [party])

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

    setStarting(true)
    setError(null)
    initializeServiceOwner(party, organizationName)
      .catch((caught: unknown) => {
        if (caught instanceof ApiError && caught.status === 409) {
          return
        }
        throw caught
      })
      .then(() => {
        wasInProgress.current = true
        setProvisioning({
          status: 'InProgress',
          percent: 40,
          messageKey: 'page.services.provision_phase_waiting',
        })
        return refresh()
      })
      .catch((caught: unknown) => {
        const body = caught instanceof ApiError ? caught.body : undefined
        setError(localizeApiError(body).message || i18n.t('page.services.provision_start_failed'))
      })
      .finally(() => {
        setStarting(false)
      })
  }, [organizationName, party, refresh, starting])

  return { provisioning, allowed, loaded, starting, error, start }
}
