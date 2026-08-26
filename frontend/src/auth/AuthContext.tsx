import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react'
import { apiFetch, redirectToLogin, redirectToLogout } from '../api/client'
import { AUTH_BASE_PATH } from '../api/config'
import type { AuthState, AuthUser, MeResponse } from './types'

type AuthContextValue = {
  status: AuthState['status']
  user: AuthUser | null
  isAuthenticated: boolean
  login: (returnUrl?: string) => void
  logout: (returnUrl?: string) => void
  refresh: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

/** null = confirmed unauthenticated; 'unavailable' = API/network failure (do not start login). */
async function fetchCurrentUser(): Promise<AuthUser | null | 'unavailable'> {
  try {
    const me = await apiFetch<MeResponse>(`${AUTH_BASE_PATH}/me`, {
      redirectOnUnauthorized: false,
    })
    // Front Door misroute: /broker hits static website → index.html instead of JSON.
    if (typeof me === 'string') {
      return 'unavailable'
    }
    if (!me?.authenticated) {
      return null
    }
    return { authenticated: true, claims: me.claims ?? [] }
  } catch {
    // Network failures and non-401 API errors — treat as unavailable, not logged-out.
    return 'unavailable'
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })

  const refresh = useCallback(async () => {
    const user = await fetchCurrentUser()
    if (user === 'unavailable') {
      setState({ status: 'api_unreachable' })
      return
    }
    setState(user ? { status: 'authenticated', user } : { status: 'unauthenticated' })
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  const value = useMemo<AuthContextValue>(
    () => ({
      status: state.status,
      user: state.status === 'authenticated' ? state.user : null,
      isAuthenticated: state.status === 'authenticated',
      login: (returnUrl) => {
        if (state.status === 'api_unreachable') {
          return
        }
        redirectToLogin(returnUrl)
      },
      logout: (returnUrl) => redirectToLogout(returnUrl),
      refresh,
    }),
    [state, refresh],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext)
  if (!ctx) {
    throw new Error('useAuth must be used within AuthProvider')
  }
  return ctx
}
