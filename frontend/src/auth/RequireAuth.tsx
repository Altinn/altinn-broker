import { useEffect, type ReactNode } from 'react'
import { useAuth } from './AuthContext'

type RequireAuthProps = {
  children: ReactNode
}

/**
 * Ensures the user has an active Broker session cookie.
 * Unauthenticated users are sent to /broker/api/v1/authentication/login.
 */
export function RequireAuth({ children }: RequireAuthProps) {
  const { status, login } = useAuth()

  useEffect(() => {
    if (status === 'unauthenticated') {
      login()
    }
  }, [status, login])

  if (status === 'api_unreachable') {
    return (
      <main id="main-content" style={{ padding: '2rem' }}>
        <p>
          Broker API er ikke tilgjengelig på denne adressen. Front Door ruter ikke{' '}
          <code>/broker</code> til APIM (fikk HTML i stedet for JSON fra{' '}
          <code>/broker/api/v1/authentication/me</code>).
        </p>
      </main>
    )
  }

  if (status === 'loading' || status === 'unauthenticated') {
    return (
      <main id="main-content" style={{ padding: '2rem' }}>
        <p>{status === 'loading' ? 'Laster innlogging…' : 'Omdirigerer til innlogging…'}</p>
      </main>
    )
  }

  return children
}
