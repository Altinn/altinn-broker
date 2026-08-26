export type AuthClaim = {
  type: string
  value: string
}

export type MeResponse = {
  authenticated: boolean
  claims: AuthClaim[]
}

export type AuthUser = {
  authenticated: true
  claims: AuthClaim[]
}

export type AuthState =
  | { status: 'loading' }
  | { status: 'authenticated'; user: AuthUser }
  | { status: 'unauthenticated' }
  /** /broker was served as SPA HTML, or /me failed for network/non-auth reasons. */
  | { status: 'api_unreachable' }

export function claimValue(user: AuthUser, type: string): string | undefined {
  return user.claims.find((c) => c.type === type)?.value
}
