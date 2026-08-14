import { RootProvider } from '@altinn/altinn-components'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import './index.css'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <RootProvider languageCode="nb">
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </RootProvider>
  </StrictMode>,
)
