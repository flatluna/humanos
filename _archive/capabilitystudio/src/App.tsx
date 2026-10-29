import { Routes, Route } from 'react-router-dom';
import Layout from './components/Layout';
import StudioAuthGate from './layouts/StudioAuthGate';
import LaunchPage from './pages/LaunchPage';
import HomePage from './pages/HomePage';
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
import NewAcademicLearningPage from './pages/NewAcademicLearningPage';
import AcademicLearningCatalogPage from './pages/AcademicLearningCatalogPage';
import StructuredLearningRunProgressPage from './pages/StructuredLearningRunProgressPage';
import StructuredLearningDocumentDetailPage from './pages/StructuredLearningDocumentDetailPage';
import StructuredLearningCoursePage from './pages/StructuredLearningCoursePage';
import StructuredLearningSectionPage from './pages/StructuredLearningSectionPage';
import StructuredLearningTopicsAdminPage from './pages/StructuredLearningTopicsAdminPage';
import StructuredLearningTopicCreatePage from './pages/StructuredLearningTopicCreatePage';
import AiAgentsPage from './pages/AiAgentsPage';
import AiAgentsDomainPage from './pages/AiAgentsDomainPage';
import AiAgentsTopicPage from './pages/AiAgentsTopicPage';
import AiAgentsChatPage from './pages/AiAgentsChatPage';
import FilmProductionsPage from './pages/FilmProductionsPage';
import VideoProductionsPage from './pages/VideoProductionsPage';
import VideoProjectsGalleryPage from './pages/VideoProjectsGalleryPage';
import ProgramEntitiesPage from './pages/ProgramEntitiesPage';
import ProgramEntitiesIndexPage from './pages/ProgramEntitiesIndexPage';
import LabsPage from './pages/LabsPage';
import LabDetailPage from './pages/LabDetailPage';
import LabBuilderPage from './pages/LabBuilderPage';

export default function App() {
  return (
    <Routes>
      {/* Outside the gate: this is the only entry point now (2026-08-19) — */}
      <Route path="/launch" element={<LaunchPage />} />
      <Route element={<StudioAuthGate />}>
        <Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/catalog" element={<HomePage />} />
          <Route path="/ai-agents" element={<AiAgentsPage />} />
          <Route path="/ai-agents/:subjectName" element={<AiAgentsDomainPage />} />
          <Route path="/ai-agents/:subjectName/:topicName" element={<AiAgentsTopicPage />} />
          <Route path="/ai-agents/:subjectName/:topicName/chat" element={<AiAgentsChatPage />} />
          <Route path="/runs/:runId" element={<RunProgressPage />} />
          <Route path="/capabilities/:capabilityId" element={<CapabilityDetailPage />} />
          <Route path="/capabilities/:capabilityId/preview" element={<PreviewGraphPage />} />
          <Route path="/capabilities/:capabilityId/preview/nodes/:nodeId" element={<PreviewNodePage />} />
          <Route path="/new-traditional" element={<NewTraditionalCoursePage />} />
          <Route path="/traditional-runs/:runId" element={<TraditionalRunProgressPage />} />
          <Route path="/capabilities/:capabilityId/traditional-course" element={<TraditionalCoursePage />} />
          <Route path="/new-structured-learning" element={<NewStructuredLearningPage />} />
          <Route path="/new-academic-learning" element={<NewAcademicLearningPage />} />
          <Route path="/academic-learning-catalog" element={<AcademicLearningCatalogPage />} />
          <Route path="/structured-learning-runs/:runId" element={<StructuredLearningRunProgressPage />} />
          <Route path="/structured-learning/:documentId" element={<StructuredLearningDocumentDetailPage />} />
          <Route path="/structured-learning/:documentId/course" element={<StructuredLearningCoursePage />} />
          <Route path="/structured-learning/:documentId/course/sections/:sectionId" element={<StructuredLearningSectionPage />} />
          <Route path="/structured-learning-topics" element={<StructuredLearningTopicsAdminPage />} />
          <Route path="/structured-learning-topics/:subjectName/new" element={<StructuredLearningTopicCreatePage />} />
          <Route path="/structured-learning-topics/:subjectName" element={<StructuredLearningTopicsAdminPage />} />
          <Route path="/programs" element={<ProgramsHomePage />} />
          <Route path="/programs/new" element={<NewProgramPage />} />
          <Route path="/programs/:programId" element={<ProgramDetailPage />} />
          <Route path="/programs/:programId/entities" element={<ProgramEntitiesPage />} />
          <Route path="/program-entities" element={<ProgramEntitiesIndexPage />} />
          <Route path="/costs" element={<CostDashboardPage />} />
          <Route path="/film-productions" element={<FilmProductionsPage />} />
          <Route path="/video-productions" element={<VideoProjectsGalleryPage />} />
          <Route path="/video-productions/new" element={<VideoProductionsPage />} />
          <Route path="/video-productions/:videoProductionId" element={<VideoProductionsPage />} />
          <Route path="/labs" element={<LabsPage />} />
          <Route path="/labs/:labId" element={<LabDetailPage />} />
          <Route path="/labs/new-ai" element={<LabBuilderPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
