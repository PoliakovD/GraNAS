const API_BASE = process.env.REACT_APP_API_URL || 'http://localhost:5002/api';

export async function fetchLogs({ service, level, correlationId, page = 1, pageSize = 50 }) {
  const params = new URLSearchParams({
    page, pageSize,
    ...(service && { service }),
    ...(level && { level }),
    ...(correlationId && { correlationId })
  });
  const response = await fetch(`${API_BASE}/logs?${params}`);
  if (!response.ok) throw new Error('Failed to fetch logs');
  return response.json();
}
