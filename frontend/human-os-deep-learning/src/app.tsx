import { BrowserRouter as Router, Routes, Route } from 'react-router-dom'
import Layout from './layout'
import Home from '@features/home'
import Capabilities from '@features/capabilities'
import Sessions from '@features/sessions'
import Evidence from '@features/evidence'
import Settings from '@features/settings'
import SessionRuntime from '@runtime/SessionRuntime'

export default function App() {
  return (
    <Router>
      <Layout>
        <Routes>
          <Route path="/" element={<Home />} />
          <Route path="/capabilities" element={<Capabilities />} />
          <Route path="/sessions" element={<Sessions />} />
          <Route path="/session/:sessionId" element={<SessionRuntime />} />
          <Route path="/evidence" element={<Evidence />} />
          <Route path="/settings" element={<Settings />} />
        </Routes>
      </Layout>
    </Router>
  )
}
