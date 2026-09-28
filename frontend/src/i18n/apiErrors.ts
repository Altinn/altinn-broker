/**
 * Localized messages for Broker API error codes shown in the SPA.
 * Keys match the numeric `errorCode` extension on ProblemDetails responses.
 */
const nbMessages: Record<number, (context: ApiErrorMessageContext) => string> = {
  36: () =>
    'Du må ha en rolle eller tilgangspakke som gir tilgangsrettigheten «publish» for denne tjenesten.',
}

export type ApiErrorMessageContext = {
  resourceId?: string
}

export type LocalizedApiError = {
  /** Numeric Broker error code from the API (`errorCode` extension), when present. */
  errorCode?: number
  message: string
  linkHref?: string
  linkLabel?: string
}

/** Builds the public tjenesteoversikten page for a resource's tilgangsrettigheter. */
export function tjenesteoversiktenResourceUrl(resourceId: string): string {
  return `https://tjenesteoversikten.no/resource/${encodeURIComponent(resourceId)}`
}

/** Notice shown when the user lacks publish access to configure a resource. */
export function publishAccessNotice(resourceId: string): LocalizedApiError {
  return {
    errorCode: 36,
    message: nbMessages[36]({ resourceId }),
    linkHref: tjenesteoversiktenResourceUrl(resourceId),
    linkLabel: 'Se tilgangsrettigheter på tjenesteoversikten.no',
  }
}

/**
 * Maps a Broker API error body to a localized UI message.
 * Falls back to the API detail text when no translation exists for the code.
 */
export function localizeApiError(
  body: unknown,
  context: ApiErrorMessageContext = {},
  locale: 'nb' = 'nb',
): LocalizedApiError {
  const errorCode = readErrorCode(body)
  const detail = readDetail(body)
  const messages = locale === 'nb' ? nbMessages : nbMessages

  if (errorCode === 36 && context.resourceId) {
    return publishAccessNotice(context.resourceId)
  }

  if (errorCode !== undefined && messages[errorCode]) {
    return {
      errorCode,
      message: messages[errorCode](context),
    }
  }

  return {
    errorCode,
    message: detail ?? 'Noe gikk galt. Prøv igjen.',
  }
}

function readErrorCode(body: unknown): number | undefined {
  if (!body || typeof body !== 'object') {
    return undefined
  }

  const record = body as Record<string, unknown>
  const direct = record.errorCode
  if (typeof direct === 'number' && Number.isFinite(direct)) {
    return direct
  }
  if (typeof direct === 'string' && /^\d+$/.test(direct)) {
    return Number(direct)
  }

  // Altinn ProblemDetails also exposes BRO-00036 as `code`.
  const code = record.code
  if (typeof code === 'string') {
    const match = /^BRO-0*(\d+)$/i.exec(code)
    if (match) {
      return Number(match[1])
    }
  }

  return undefined
}

function readDetail(body: unknown): string | undefined {
  if (!body || typeof body !== 'object') {
    return undefined
  }
  const detail = (body as { detail?: unknown }).detail
  return typeof detail === 'string' && detail.trim() !== '' ? detail : undefined
}
