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
  /** /broker was served as the SPA HTML instead of the API (Front Door routing). */
  | { status: 'api_unreachable' }

export function claimValue(user: AuthUser, type: string): string | undefined {
  return user.claims.find((c) => c.type === type)?.value
}
