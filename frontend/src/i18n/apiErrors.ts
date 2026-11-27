/**
 * Localized messages for Broker API error codes shown in the SPA.
 * Keys match the numeric `errorCode` extension on ProblemDetails responses.
 */
import { i18n } from './config'

export const BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID = 'digdir-broker-administrasjon'

const errorMessageKey: Record<number, string> = {
  13: 'page.errors.max_size_with_virus',
  36: 'page.errors.gatekeeper',
  37: 'page.errors.must_be_owner',
  38: 'page.errors.wrong_party',
}

export type ApiErrorMessageContext = {
  resourceId?: string
}

export type LocalizedApiError = {
  /** Numeric Broker error code from the API (`errorCode` extension), when present. */
  errorCode?: number
  /**
   * Plain message. When <c>linkHref</c> is set with <c>linkInMessage</c>, the link label
   * is meant to appear inline where the gatekeeper resource id is mentioned.
   */
  message: string
  linkHref?: string
  linkLabel?: string
  /** When true, render <c>linkLabel</c> inline inside the message (replace the label text). */
  linkInMessage?: boolean
}

/** Builds the public tjenesteoversikten page for a resource's tilgangsrettigheter. */
export function tjenesteoversiktenResourceUrl(resourceId: string): string {
  return `https://tjenesteoversikten.no/resource/${encodeURIComponent(resourceId)}`
}

/**
 * Notice when the user cannot edit Broker configuration.
 * Non–service-owners get a short ownership message; service owners without
 * gatekeeper publish get a message with a tjenesteoversikten link on the resource id.
 */
export function configureAccessNotice(isServiceOwner: boolean): LocalizedApiError {
  if (!isServiceOwner) {
    return {
      errorCode: 37,
      message: i18n.t(errorMessageKey[37]),
    }
  }

  return {
    errorCode: 36,
    message: i18n.t(errorMessageKey[36]),
    linkHref: tjenesteoversiktenResourceUrl(BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID),
    linkLabel: BROKERBOX_CONFIGURE_GATEKEEPER_RESOURCE_ID,
    linkInMessage: true,
  }
}

/** @deprecated Prefer {@link configureAccessNotice}. */
export function publishAccessNotice(_resourceId?: string): LocalizedApiError {
  return configureAccessNotice(true)
}

/**
 * Maps a Broker API error body to a localized UI message.
 * Falls back to the API detail text when no translation exists for the code.
 */
export function localizeApiError(
  body: unknown,
  context: ApiErrorMessageContext = {},
): LocalizedApiError {
  const errorCode = readErrorCode(body)
  const detail = readDetail(body)

  if (errorCode === 36) {
    return configureAccessNotice(true)
  }

  if (errorCode === 37) {
    return configureAccessNotice(false)
  }

  const messageKey = errorCode !== undefined ? errorMessageKey[errorCode] : undefined
  if (messageKey) {
    return {
      errorCode,
      message: i18n.t(messageKey, context),
    }
  }

  return {
    errorCode,
    message: detail ?? i18n.t('page.errors.generic'),
  }
}

/** Renders a localized error that may contain an inline link (gatekeeper resource id). */
export function messageWithOptionalInlineLink(error: LocalizedApiError): {
  before: string
  link?: { href: string; label: string }
  after: string
} {
  if (!error.linkInMessage || !error.linkHref || !error.linkLabel) {
    return { before: error.message, after: '' }
  }

  const index = error.message.indexOf(error.linkLabel)
  if (index < 0) {
    return { before: error.message, link: { href: error.linkHref, label: error.linkLabel }, after: '' }
  }

  return {
    before: error.message.slice(0, index),
    link: { href: error.linkHref, label: error.linkLabel },
    after: error.message.slice(index + error.linkLabel.length),
  }
}

function readErrorCode(body: unknown): number | undefined {
  if (!body || typeof body !== 'object') {
    return undefined
  }

  const record = body as Record<string, unknown>
  const direct = record.errorCode
  if (typeof direct === 'number' && Number.isFinite(direct)) {
    return Number(direct)
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
