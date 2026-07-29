import { api } from './api';
import type {
  Lease,
  PortfolioAnalytics,
  PropertyEvent,
  PropertyMetrics,
  PropertyTransaction,
  PropertyValuation,
  RentPricePoint,
  RentalProperty,
} from '../models/models';

/** Fields the server accepts when creating or editing a property. */
export interface RentalPropertyRequest {
  propertyName: string;
  address: string;
  city?: string | null;
  postalCode?: string | null;
  countryCode?: string | null;
  propertyType: number;
  sizeSqm?: number | null;
  bedrooms?: number | null;
  purchasePrice?: number | null;
  purchaseDate?: string | null;
  status: number;
  salePrice?: number | null;
  saleDate?: string | null;
  notes?: string | null;
  currencyCode?: string | null;
}

export async function createProperty(request: RentalPropertyRequest): Promise<RentalProperty> {
  const response = await api.post<RentalProperty>('/RentalProperties', request);
  return response.data;
}

export async function updateProperty(id: number, request: RentalPropertyRequest): Promise<void> {
  await api.put(`/RentalProperties/${id}`, request);
}

export async function fetchProperty(id: number): Promise<RentalProperty> {
  const response = await api.get<RentalProperty>(`/RentalProperties/${id}`);
  return response.data;
}

export async function fetchPropertyMetrics(id: number): Promise<PropertyMetrics> {
  const response = await api.get<PropertyMetrics>(`/RentalProperties/${id}/analytics`);
  return response.data;
}

export async function fetchPortfolioAnalytics(): Promise<PortfolioAnalytics> {
  const response = await api.get<PortfolioAnalytics>('/RentalProperties/analytics/portfolio');
  return response.data;
}

export async function fetchTransactions(propertyId: number): Promise<PropertyTransaction[]> {
  const response = await api.get<PropertyTransaction[]>(`/RentalProperties/${propertyId}/transactions`);
  return response.data;
}

export interface TransactionRequest {
  date: string;
  amount: number;
  category: number;
  description?: string | null;
}

export async function createTransaction(
  propertyId: number,
  request: TransactionRequest
): Promise<PropertyTransaction> {
  const response = await api.post<PropertyTransaction>(
    `/RentalProperties/${propertyId}/transactions`,
    request
  );
  return response.data;
}

export async function deleteTransaction(propertyId: number, id: number): Promise<void> {
  await api.delete(`/RentalProperties/${propertyId}/transactions/${id}`);
}

export async function fetchLeases(propertyId: number): Promise<Lease[]> {
  const response = await api.get<Lease[]>(`/RentalProperties/${propertyId}/leases`);
  return response.data;
}

export interface LeaseRequest {
  tenantName: string;
  startDate: string;
  monthlyRent: number;
  endDate?: string | null;
  tenantEmail?: string | null;
  tenantPhone?: string | null;
  rentDueDayOfMonth: number;
  depositAmount?: number | null;
  notes?: string | null;
}

export async function createLease(propertyId: number, request: LeaseRequest): Promise<Lease> {
  const response = await api.post<Lease>(`/RentalProperties/${propertyId}/leases`, request);
  return response.data;
}

export async function fetchRentHistory(propertyId: number): Promise<RentPricePoint[]> {
  const response = await api.get<RentPricePoint[]>(`/RentalProperties/${propertyId}/rent-history`);
  return response.data;
}

export async function addMarketEstimate(
  propertyId: number,
  amount: number,
  effectiveFrom?: string
): Promise<RentPricePoint> {
  const response = await api.post<RentPricePoint>(
    `/RentalProperties/${propertyId}/rent-history/market-estimate`,
    { amount, effectiveFrom }
  );
  return response.data;
}

export async function fetchValuations(propertyId: number): Promise<PropertyValuation[]> {
  const response = await api.get<PropertyValuation[]>(`/RentalProperties/${propertyId}/valuations`);
  return response.data;
}

export async function createValuation(
  propertyId: number,
  valuedOn: string,
  value: number,
  source = 1
): Promise<PropertyValuation> {
  const response = await api.post<PropertyValuation>(
    `/RentalProperties/${propertyId}/valuations`,
    { valuedOn, value, source }
  );
  return response.data;
}

export async function fetchPropertyEvents(propertyId: number): Promise<PropertyEvent[]> {
  const response = await api.get<PropertyEvent[]>(`/RentalProperties/${propertyId}/events`);
  return response.data;
}
