import { useEffect, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useAuth } from './AuthContext'

type RequireAuthProps = {
  children: ReactNode
}

/**
 * Ensures the user has an active Broker session cookie.
 * Unauthenticated users are sent to /broker/api/v1/authentication/login.
 */
export function RequireAuth({ children }: RequireAuthProps) {
  const { t } = useTranslation()
  const { status, login } = useAuth()

  useEffect(() => {
    if (status === 'unauthenticated') {
      login()
    }
  }, [status, login])

  if (status === 'api_unreachable') {
    return (
      <main id="main-content" style={{ padding: '2rem' }}>
        <p>{t('page.auth.unreachable')}</p>
      </main>
    )
  }

  if (status === 'loading' || status === 'unauthenticated') {
    return (
      <main id="main-content" style={{ padding: '2rem' }}>
        <p>{status === 'loading' ? t('page.auth.loading') : t('page.auth.redirecting')}</p>
      </main>
    )
  }

  return children
}
