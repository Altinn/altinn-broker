export type Organization = {
  name: string
  orgNumber: string
}

export type FileTransferService = {
  id: string
  name: string
  owner: string
  canCreate: boolean
  lockedVariables?: { name: string; description: string }[]
}

export type Transfer = {
  id: string
  serviceId: string
  serviceName: string
  reference: string
  subtitle: string
  sender: Organization
  recipient: Organization
  fileName: string
  fileSize: string
  createdAt: string
  uploadedAt?: string
  downloadedAt?: string
  virusScanned?: string
  status?: string
  statusNote?: string
  otherMetadata?: string
}

export const currentOrganization: Organization = {
  name: 'Brønnøy sykehus',
  orgNumber: '922 194 912',
}

export const organizations: Organization[] = [
  currentOrganization,
  { name: 'Sandnessjøen sykehus', orgNumber: '985 616 167' },
  { name: 'St. Olavs hospital', orgNumber: '985 627 706' },
  { name: 'Haukeland sykehus', orgNumber: '889 640 782' },
  { name: 'Lovisenberg diakonale sykehus', orgNumber: '985 399 077' },
  { name: 'Arbeidstilsynet', orgNumber: '974 761 211' },
  { name: 'Oslo universitetssykehus', orgNumber: '974 760 673' },
]

export const fileTransferServices: FileTransferService[] = [
  {
    id: 'rontgen',
    name: 'Røntgenbilder mellom sykehus',
    owner: 'Helsedirektoratet',
    canCreate: true,
    lockedVariables: [
      {
        name: 'maxFileTransferSize = 50 GB',
        description: 'Grense for hvor stort et vedlegg kan være',
      },
      {
        name: 'virusScanRequired = true',
        description: 'Filen skal virusskannes før mottaker kan laste ned',
      },
    ],
  },
  {
    id: 'avvik',
    name: 'Avviksrapport til Arbeidstilsynet',
    owner: 'Arbeidstilsynet',
    canCreate: false,
  },
  {
    id: 'forskning',
    name: 'Forskningsresultater OUS',
    owner: 'Oslo universitetssykehus',
    canCreate: false,
  },
]

export const activeTransfers: Transfer[] = [
  {
    id: 'active-1',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2026/1235262',
    subtitle: 'Brønnøy sykehus - St. Olavs Hospital - 2026/1235262',
    sender: currentOrganization,
    recipient: { name: 'St. Olavs hospital', orgNumber: '985 627 706' },
    fileName: 'Pasient h564hj6j4h345 - Venstre kne.dicom',
    fileSize: '2,73 GB',
    createdAt: '12. august 2026 klokka 12:29',
    uploadedAt: '12. august 2026 klokka 12:59',
    virusScanned: 'Utført',
    status: 'Venter på nedlasting. Må gjøres innen 11. september 2026 klokka 12:59',
    statusNote: 'Kan bare utføres av St. Olavs hospital',
  },
  {
    id: 'active-2',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2026/7134252',
    subtitle: 'Brønnøy sykehus - St. Olavs Hospital - 2026/7134252',
    sender: currentOrganization,
    recipient: { name: 'St. Olavs hospital', orgNumber: '985 627 706' },
    fileName: 'Pasient abc123 - Høyre skulder.dicom',
    fileSize: '1,12 GB',
    createdAt: '10. august 2026 klokka 09:15',
    uploadedAt: '10. august 2026 klokka 09:45',
    virusScanned: 'Utført',
    status: 'Venter på nedlasting. Må gjøres innen 9. september 2026 klokka 09:45',
    statusNote: 'Kan bare utføres av St. Olavs hospital',
  },
  {
    id: 'active-3',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2025/4443333',
    subtitle: 'Haukeland sykehus - Brønnøy sykehus - 2025/4443333',
    sender: { name: 'Haukeland sykehus', orgNumber: '889 640 782' },
    recipient: currentOrganization,
    fileName: 'Pasient xyz789 - Thorax.dicom',
    fileSize: '890 MB',
    createdAt: '5. august 2026 klokka 14:00',
    uploadedAt: '5. august 2026 klokka 14:30',
    virusScanned: 'Utført',
    status: 'Venter på nedlasting. Må gjøres innen 4. september 2026 klokka 14:30',
    statusNote: 'Kan bare utføres av Brønnøy sykehus',
  },
  {
    id: 'active-4',
    serviceId: 'avvik',
    serviceName: 'Avviksrapport til Arbeidstilsynet',
    reference: 'Hendelse 4. april 2026',
    subtitle: 'Brønnøy sykehus - Arbeidstilsynet - Hendelse 4. april 2026',
    sender: currentOrganization,
    recipient: { name: 'Arbeidstilsynet', orgNumber: '974 761 211' },
    fileName: 'Avviksrapport-april-2026.pdf',
    fileSize: '4,2 MB',
    createdAt: '4. april 2026 klokka 16:00',
    uploadedAt: '4. april 2026 klokka 16:05',
    virusScanned: 'Utført',
    status: 'Venter på nedlasting',
    statusNote: 'Kan bare utføres av Arbeidstilsynet',
  },
  {
    id: 'active-5',
    serviceId: 'forskning',
    serviceName: 'Forskningsresultater OUS',
    reference: 'Avhandling kandidat 2054-RST/1',
    subtitle: 'Brønnøy sykehus - Oslo universitetssykehus - Avhandling kandidat 2054-RST/1',
    sender: currentOrganization,
    recipient: { name: 'Oslo universitetssykehus', orgNumber: '974 760 673' },
    fileName: 'Avhandling-2054-RST-1.pdf',
    fileSize: '12,8 MB',
    createdAt: '1. august 2026 klokka 11:00',
    uploadedAt: '1. august 2026 klokka 11:10',
    virusScanned: 'Utført',
    status: 'Venter på nedlasting',
    statusNote: 'Kan bare utføres av Oslo universitetssykehus',
  },
]

export const historicalTransfers: Transfer[] = [
  {
    id: 'hist-1',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2024/1234567',
    subtitle: 'Brønnøy sykehus - Haukeland sykehus - 2024/1234567',
    sender: currentOrganization,
    recipient: { name: 'Haukeland sykehus', orgNumber: '889 640 782' },
    fileName: 'Pasient old001 - Cranium.dicom',
    fileSize: '1,45 GB',
    createdAt: '15. januar 2025 klokka 10:00',
    uploadedAt: '15. januar 2025 klokka 10:30',
    downloadedAt: '16. januar 2025 klokka 08:15',
    virusScanned: 'Utført',
    status: 'Fullført',
  },
  {
    id: 'hist-2',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2024/7654321',
    subtitle: 'Brønnøy sykehus - Haukeland sykehus - 2024/7654321',
    sender: currentOrganization,
    recipient: { name: 'Haukeland sykehus', orgNumber: '889 640 782' },
    fileName: 'Pasient old002 - Abdomen.dicom',
    fileSize: '2,10 GB',
    createdAt: '20. februar 2025 klokka 14:00',
    uploadedAt: '20. februar 2025 klokka 14:45',
    downloadedAt: '21. februar 2025 klokka 09:00',
    virusScanned: 'Utført',
    status: 'Fullført',
  },
  {
    id: 'hist-3',
    serviceId: 'rontgen',
    serviceName: 'Røntgenbilder mellom sykehus',
    reference: '2025/1515151',
    subtitle: 'Brønnøy sykehus - Haukeland sykehus - 2025/1515151',
    sender: currentOrganization,
    recipient: { name: 'Haukeland sykehus', orgNumber: '889 640 782' },
    fileName: 'Pasient old003 - Pelvis.dicom',
    fileSize: '980 MB',
    createdAt: '10. mars 2025 klokka 11:30',
    uploadedAt: '10. mars 2025 klokka 12:00',
    downloadedAt: '11. mars 2025 klokka 15:20',
    virusScanned: 'Utført',
    status: 'Fullført',
  },
  {
    id: 'hist-4',
    serviceId: 'avvik',
    serviceName: 'Avviksrapport til Arbeidstilsynet',
    reference: 'Turnuskandidat ruset på lystgass',
    subtitle: 'Brønnøy sykehus - Arbeidstilsynet - Turnuskandidat ruset på lystgass',
    sender: currentOrganization,
    recipient: { name: 'Arbeidstilsynet', orgNumber: '974 761 211' },
    fileName: 'Julebordsvideo.zip',
    fileSize: '4,93 GB',
    createdAt: '13. desember 2025 klokka 23:30',
    uploadedAt: '2. januar 2026 klokka 08:45',
    downloadedAt: '3. januar 2026 klokka 10:00',
    virusScanned: 'Utført',
    status: 'Fullført',
  },
]

export function getServiceById(id: string) {
  return fileTransferServices.find((s) => s.id === id)
}

export function getActiveTransferById(id: string) {
  return activeTransfers.find((t) => t.id === id)
}

export function getHistoricalTransferById(id: string) {
  return historicalTransfers.find((t) => t.id === id)
}

export function formatOrganization(org: Organization) {
  return `${org.name} - Org.nr. ${org.orgNumber}`
}
