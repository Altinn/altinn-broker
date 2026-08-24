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

async function fetchCurrentUser(): Promise<AuthUser | null> {
  try {
    const me = await apiFetch<MeResponse>('/authentication/me', {
      redirectOnUnauthorized: false,
    })
    if (!me?.authenticated) {
      return null
    }
    return { authenticated: true, claims: me.claims ?? [] }
  } catch {
    return null
  }
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<AuthState>({ status: 'loading' })

  const refresh = useCallback(async () => {
    const user = await fetchCurrentUser()
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
      login: (returnUrl) => redirectToLogin(returnUrl),
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
