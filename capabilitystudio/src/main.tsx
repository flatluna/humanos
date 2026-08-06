import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { MsalProvider } from '@azure/msal-react';
import { EventType } from '@azure/msal-browser';
import App from './App';
import { msalInstance } from './auth';
import { AuthProvider } from './auth/AuthContext';
import './index.css';
import './i18n';

msalInstance.initialize().then(() => {
  msalInstance.handleRedirectPromise();

  const activeAccount = msalInstance.getActiveAccount() ?? msalInstance.getAllAccounts()[0];
  if (activeAccount) msalInstance.setActiveAccount(activeAccount);

  msalInstance.addEventCallback((event) => {
    if (event.eventType === EventType.LOGIN_SUCCESS && event.payload) {
      const account = (event.payload as { account?: import('@azure/msal-browser').AccountInfo }).account;
      if (account) msalInstance.setActiveAccount(account);
    }
  });

  ReactDOM.createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <MsalProvider instance={msalInstance}>
        <AuthProvider>
          <BrowserRouter>
            <App />
          </BrowserRouter>
        </AuthProvider>
      </MsalProvider>
    </React.StrictMode>
  );
});
