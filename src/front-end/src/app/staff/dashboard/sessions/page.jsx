"use client";
import { ErrorBoundary } from "react-error-boundary";
import SessionDashboard from "@/components/staff/SessionDashboard";
import styled from "@emotion/styled";

const ErrorMessage = styled.div`
  padding: 16px;
  color: red;
`;

const FullPageContainer = styled.div`
  overflow-y: auto;
`;

function ErrorFallback({ error }) {
  return (
    <ErrorMessage>
      <h3>Something went wrong:</h3>
      <pre>{error.message}</pre>
    </ErrorMessage>
  );
}

const SessionsPage = () => (
  <FullPageContainer>
    <ErrorBoundary FallbackComponent={ErrorFallback}>
      <SessionDashboard />
    </ErrorBoundary>
  </FullPageContainer>
);

export default SessionsPage;
