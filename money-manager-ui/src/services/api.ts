import axios from 'axios';
import type { UpcomingEvent } from '../models/models';

export const api = axios.create({
  baseURL: 'http://localhost:5296/api', // adjust port if needed
});

export async function fetchUpcomingEvents(): Promise<UpcomingEvent[]> {
    const response = await api.get<UpcomingEvent[]>('/UpcomingEvents');
    return response.data;
}