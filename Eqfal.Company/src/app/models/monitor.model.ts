export interface SystemHealth {
  status: string;
  apiStatus: string;
  databaseStatus: string;
  nodeStatus: string;
  timestamp: string;
  nodeHealth?: {
    status: string;
    apiStatus: string;
    uptimeSeconds: number;
    memoryUsage: {
      rss: number;
      heapTotal: number;
      heapUsed: number;
      external: number;
      arrayBuffers: number;
    };
    activeSessionsCount: number;
    authStoresCount: number;
    webhookQueueLength: number;
    activePairingsCount: number;
    sessionStates: { [key: string]: string };
  };
}

export interface SystemMetrics {
  messagesSent: number;
  messagesReceived: number;
  webhookSuccess: number;
  webhookFailures: number;
  webhookDropped: number;
  pairingAttempts: number;
  reconnectAttempts: number;
  startedAt: string;
  activeSessions: number;
  authStoresCount: number;
  webhookQueueLength: number;
  activeWebhookWorkers: number;
  activePairingsCount: number;
}

export interface UserSession {
  userId: number;
  fullName: string;
  phone: string;
  status: string;
  lastConnected: string | null;
}

export interface SystemLog {
  id: string;
  timestamp: string;
  type: 'INFO' | 'SUCCESS' | 'WARN' | 'ERROR' | 'FATAL';
  message: string;
  service: string;
}
