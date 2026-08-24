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

  if (status === 'loading' || status === 'unauthenticated') {
    return (
      <main id="main-content" style={{ padding: '2rem' }}>
        <p>{status === 'loading' ? 'Laster innlogging…' : 'Omdirigerer til innlogging…'}</p>
      </main>
    )
  }

  return children
}
