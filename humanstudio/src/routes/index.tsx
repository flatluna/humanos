import { BrowserRouter, Routes, Route } from 'react-router-dom'
import { AppShell } from '../components/layout'
import { Home, Studio, CapabilityLibraryPage, Progress, Settings, NotFound, StudioGenerationPage } from '../pages'
import { BlueprintStep } from '../components/studio'
import { StudioFinalReviewPage } from '../pages/StudioFinalReviewPage'
import { StudioPublicationPage } from '../pages/StudioPublicationPage'
import { CapabilityDetailPage } from '../pages/CapabilityDetailPage'

export function AppRoutes() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<Home />} />
          <Route path="/studio" element={<Studio />} />
          <Route path="/studio/blueprint" element={<BlueprintStep />} />
          <Route path="/studio/runs/:runId/generation" element={<StudioGenerationPage />} />
          <Route path="/studio/runs/:runId/review" element={<StudioFinalReviewPage />} />
          <Route path="/studio/runs/:runId/publishing" element={<StudioPublicationPage />} />
          <Route path="/capabilities" element={<CapabilityLibraryPage />} />
          <Route path="/capabilities/:capabilityId" element={<CapabilityDetailPage />} />
          <Route path="/progress" element={<Progress />} />
          <Route path="/settings" element={<Settings />} />
          {/* Fallback for invalid routes */}
          <Route path="*" element={<NotFound />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

