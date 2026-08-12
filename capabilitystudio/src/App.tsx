import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import StudioAuthGate from './layouts/StudioAuthGate';
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
import NewTraditionalCoursePage from './pages/NewTraditionalCoursePage';
import TraditionalRunProgressPage from './pages/TraditionalRunProgressPage';
import TraditionalCoursePage from './pages/TraditionalCoursePage';
import NewStructuredLearningPage from './pages/NewStructuredLearningPage';
import StructuredLearningRunProgressPage from './pages/StructuredLearningRunProgressPage';
import StructuredLearningDocumentDetailPage from './pages/StructuredLearningDocumentDetailPage';
import StructuredLearningCoursePage from './pages/StructuredLearningCoursePage';
import StructuredLearningSectionPage from './pages/StructuredLearningSectionPage';

export default function App() {
  return (
    <Routes>
      <Route element={<StudioAuthGate />}>
        <Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/new" element={<NewCapabilityPage />} />
          <Route path="/runs/:runId" element={<RunProgressPage />} />
          <Route path="/capabilities/:capabilityId" element={<CapabilityDetailPage />} />
          <Route path="/capabilities/:capabilityId/preview" element={<PreviewGraphPage />} />
          <Route path="/capabilities/:capabilityId/preview/nodes/:nodeId" element={<PreviewNodePage />} />
          <Route path="/new-traditional" element={<NewTraditionalCoursePage />} />
          <Route path="/traditional-runs/:runId" element={<TraditionalRunProgressPage />} />
          <Route path="/capabilities/:capabilityId/traditional-course" element={<TraditionalCoursePage />} />
          <Route path="/new-structured-learning" element={<NewStructuredLearningPage />} />
          <Route path="/structured-learning-runs/:runId" element={<StructuredLearningRunProgressPage />} />
          <Route path="/structured-learning/:documentId" element={<StructuredLearningDocumentDetailPage />} />
          <Route path="/structured-learning/:documentId/course" element={<StructuredLearningCoursePage />} />
          <Route path="/structured-learning/:documentId/course/sections/:sectionId" element={<StructuredLearningSectionPage />} />
          <Route path="/programs" element={<ProgramsHomePage />} />
          <Route path="/programs/new" element={<NewProgramPage />} />
          <Route path="/programs/:programId" element={<ProgramDetailPage />} />
          <Route path="/costs" element={<CostDashboardPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
