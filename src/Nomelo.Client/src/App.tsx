import { Route, Routes } from "react-router-dom";
import { AuthGate } from "./auth/AuthGate";
import { HomePage } from "./routes/HomePage";
import { VotingPage } from "./routes/VotingPage";
import { ResultsPage } from "./routes/ResultsPage";
import { SharedResultsPage } from "./routes/SharedResultsPage";

export default function App() {
  return (
    <Routes>
      <Route path="/share/:token" element={<SharedResultsPage />} />
      <Route
        path="/*"
        element={
          <AuthGate>
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/sessions/:id" element={<VotingPage />} />
              <Route path="/sessions/:id/results" element={<ResultsPage />} />
              <Route path="*" element={<div>Page introuvable</div>} />
            </Routes>
          </AuthGate>
        }
      />
    </Routes>
  );
}
