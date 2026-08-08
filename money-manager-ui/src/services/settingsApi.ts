import { api } from './api';
import type { Settings } from '../models/models';

export async function fetchSettings(): Promise<Settings> {
  const response = await api.get<Settings>('/Settings');
  return response.data;
}

export async function updateSettings(settings: Settings): Promise<Settings> {
  const response = await api.put<Settings>('/Settings', settings);
  return response.data;
}
