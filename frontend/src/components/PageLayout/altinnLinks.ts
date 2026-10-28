/**
 * Shared Altinn destinations, following arbeidsflate
 * (dialogporten-frontend `auth/url.ts`) so the shell links resolve per environment.
 */

type HostEnv = 'local' | 'at23' | 'tt02' | 'yt' | 'prod'

function getEnvByHost(): HostEnv {
  const host = window.location.host
  if (host.includes('localhost')) {
    return 'local'
  }
  if (host.includes('at.altinn.cloud') || host.includes('at23.altinn.cloud')) {
    return 'at23'
  }
  if (host.includes('yt.altinn.cloud')) {
    return 'yt'
  }
  if (host.includes('tt.altinn.no') || host.includes('tt02.altinn.no')) {
    return 'tt02'
  }
  return 'prod'
}

const INFO_PORTAL_HOST: Record<HostEnv, string> = {
  local: 'https://info.at23.altinn.cloud',
  at23: 'https://info.at23.altinn.cloud',
  tt02: 'https://info.tt02.altinn.no',
  yt: 'https://info.tt02.altinn.no',
  prod: 'https://info.altinn.no',
}

const ACCESS_MANAGEMENT_HOST: Record<HostEnv, string> = {
  local: 'https://am.ui.at23.altinn.cloud/accessmanagement/ui',
  at23: 'https://am.ui.at23.altinn.cloud/accessmanagement/ui',
  tt02: 'https://am.ui.tt02.altinn.no/accessmanagement/ui',
  // Arbeidsflate has no access-management UI in yt and points there at at23.
  yt: 'https://am.ui.at23.altinn.cloud/accessmanagement/ui',
  prod: 'https://am.ui.altinn.no/accessmanagement/ui',
}

const ARBEIDSFLATE_HOST: Record<HostEnv, string> = {
  local: 'https://af.at23.altinn.cloud',
  at23: 'https://af.at23.altinn.cloud',
  tt02: 'https://af.tt02.altinn.no',
  yt: 'https://af.at23.altinn.cloud',
  prod: 'https://af.altinn.no',
}

type LinkPaths = { nb: string; en: string; nn: string }

function createInfoPortalLink(paths: LinkPaths, language = 'nb') {
  let path = paths.nb
  if (language === 'en') {
    path = paths.en
  } else if (language === 'nn') {
    path = paths.nn
  }
  return INFO_PORTAL_HOST[getEnvByHost()] + path
}

export function getFrontPageLink(language = 'nb') {
  const base = INFO_PORTAL_HOST[getEnvByHost()]
  if (language === 'en') {
    return `${base}/en`
  }
  if (language === 'nn') {
    return `${base}/nn`
  }
  return base
}

export function getAccessManagementLink() {
  return ACCESS_MANAGEMENT_HOST[getEnvByHost()]
}

export function getArbeidsflateLink(path: string) {
  const normalized = path.startsWith('/') ? path : `/${path}`
  return ARBEIDSFLATE_HOST[getEnvByHost()] + normalized
}

export function getNewFormLink(language = 'nb') {
  return createInfoPortalLink(
    { nb: '/skjemaoversikt/', en: '/en/forms-overview/', nn: '/nn/skjemaoversikt/' },
    language,
  )
}

export function getStartNewBusinessLink(language = 'nb') {
  return createInfoPortalLink(
    { nb: '/starte-og-drive/', en: '/en/start-and-run-business/', nn: '/nn/starte-og-drive/' },
    language,
  )
}

export function getNeedHelpLink(language = 'nb') {
  return createInfoPortalLink({ nb: '/hjelp/', en: '/en/help/', nn: '/nn/hjelp/' }, language)
}

export function getHelpPagesLink(language = 'nb') {
  return createInfoPortalLink(
    { nb: '/hjelp/ny-innboks-beta/', en: '/en/help/inbox/', nn: '/nn/hjelp/innboks/' },
    language,
  )
}

export function getAboutAltinnLink(language = 'nb') {
  return createInfoPortalLink(
    { nb: '/om-altinn/', en: '/en/about-altinn/', nn: '/nn/om-altinn/' },
    language,
  )
}

export function getAnnouncementsLink(language = 'nb') {
  return createInfoPortalLink(
    {
      nb: '/om-altinn/driftsmeldinger/',
      en: '/en/about-altinn/service-announcements/',
      nn: '/nn/om-altinn/driftsmeldingar/',
    },
    language,
  )
}

export function getPrivacyLink(language = 'nb') {
  return createInfoPortalLink(
    { nb: '/om-altinn/personvern/', en: '/en/about-altinn/privacy/', nn: '/nn/om-altinn/personvern/' },
    language,
  )
}

export function getAccessibilityLink(language = 'nb') {
  return createInfoPortalLink(
    {
      nb: '/om-altinn/tilgjengelighet/',
      en: '/en/about-altinn/tilgjengelighet/',
      nn: '/nn/om-altinn/tilgjengelighet/',
    },
    language,
  )
}

/** Renewal article shown in the layout banner, same as arbeidsflate. */
export function getBannerLink(language = 'nb') {
  if (language === 'en') {
    return 'https://info.altinn.no/en/news/new-power-of-attorney-solution/'
  }
  if (language === 'nn') {
    return 'https://info.altinn.no/nn/nyheiter/ny-fullmaktsloeysing/'
  }
  return 'https://info.altinn.no/nyheter/ny-fullmaktsloesning/'
}
