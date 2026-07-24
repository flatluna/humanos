import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import HomePage from './pages/HomePage';
import NewCapabilityPage from './pages/NewCapabilityPage';
import RunProgressPage from './pages/RunProgressPage';
import CapabilityDetailPage from './pages/CapabilityDetailPage';
import PreviewGraphPage from './pages/PreviewGraphPage';
import PreviewNodePage from './pages/PreviewNodePage';
import ProgramsHomePage from './pages/ProgramsHomePage';
import NewProgramPage from './pages/NewProgramPage';
import ProgramDetailPage from './pages/ProgramDetailPage';
import CostDashboardPage from './pages/CostDashboardPage';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<HomePage />} />
        <Route path="/new" element={<NewCapabilityPage />} />
        <Route path="/runs/:runId" element={<RunProgressPage />} />
        <Route path="/capabilities/:capabilityId" element={<CapabilityDetailPage />} />
        <Route path="/capabilities/:capabilityId/preview" element={<PreviewGraphPage />} />
        <Route path="/capabilities/:capabilityId/preview/nodes/:nodeId" element={<PreviewNodePage />} />
        <Route path="/programs" element={<ProgramsHomePage />} />
        <Route path="/programs/new" element={<NewProgramPage />} />
        <Route path="/programs/:programId" element={<ProgramDetailPage />} />
        <Route path="/costs" element={<CostDashboardPage />} />
      </Route>
    </Routes>
  );
}
