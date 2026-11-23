import { useCallback, useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import type { AuthorizedResource } from '../../api/resources'
import {
  fetchServiceOwnerProvisioning,
  startServiceOwnerProvisioning,
  type ServiceOwnerProvisioning,
} from '../../api/serviceOwnerProvisioning'
import { localizeApiError } from '../../i18n/apiErrors'
import { i18n } from '../../i18n/config'

const POLL_INTERVAL_MS = 3000

export function provisioningFromResource(resource: AuthorizedResource): ServiceOwnerProvisioning {
  return {
    status: resource.provisioningStatus,
    phase: resource.provisioningPhase,
    percent: resource.provisioningPercent,
  }
}

/**
 * Tracks service-owner provisioning for the selected party and polls while deployment runs.
 * `onReady` runs once when deployment finishes, so the resource list can be loaded again.
 */
export function useServiceOwnerProvisioning(
  party: string | undefined,
  organizationName: string,
  snapshot: ServiceOwnerProvisioning | null,
  onReady: () => void,
  poll: boolean,
) {
  const [state, setState] = useState<ServiceOwnerProvisioning | null>(snapshot)
  const [starting, setStarting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    setState(snapshot)
    setStarting(false)
    setError(null)
  }, [party, snapshot?.status, snapshot?.phase, snapshot?.percent])

  useEffect(() => {
    if (!party || !poll || state?.status !== 'InProgress') {
      return
    }

    let cancelled = false
    const refresh = () => {
      fetchServiceOwnerProvisioning(party)
        .then((next) => {
          if (cancelled) {
            return
          }
          setState(next)
          setError(null)
          if (next.status === 'Ready') {
            onReady()
          }
        })
        .catch(() => {
          if (!cancelled) {
            setError(i18n.t('page.services.provision_poll_failed'))
          }
        })
    }

    refresh()
    const timer = window.setInterval(refresh, POLL_INTERVAL_MS)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [party, poll, state?.status, onReady])

  const start = useCallback(() => {
    if (!party || starting) {
      return
    }

    setStarting(true)
    setError(null)
    startServiceOwnerProvisioning(party, organizationName)
      .then((next) => {
        setState(next)
        if (next.status === 'Ready') {
          onReady()
        }
      })
      .catch((caught: unknown) => {
        const body = caught instanceof ApiError ? caught.body : undefined
        setError(localizeApiError(body).message || i18n.t('page.services.provision_start_failed'))
      })
      .finally(() => {
        setStarting(false)
      })
  }, [organizationName, onReady, party, starting])

  return { provisioning: state ?? snapshot, starting, error, start }
}
