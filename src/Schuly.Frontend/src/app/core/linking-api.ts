import { HttpClient } from '@angular/common/http';
import { Service, inject } from '@angular/core';
import { Observable } from 'rxjs';

/*
 * Hand-written DTOs for /api/linking/** (linking contract section 2).
 * Swap for the generated client in src/app/api once the server OpenAPI is regenerated.
 */

export type LinkPlatformName = 'web' | 'mobile';
export type LinkStepKind = 'form' | 'otp' | 'pushCode' | 'deviceCode' | 'redirect' | 'select' | 'done' | 'error';
export type LinkFieldType = 'text' | 'email' | 'password' | 'select' | 'url' | 'totpSecret';
export type LinkSessionStatus = 'active' | 'done' | 'failed' | 'expired';
export type LinkedAccountStatus = 'ok' | 'expired' | 'error' | 'syncing';

export interface LinkIconDto {
  lucide?: string | null;
  svgUrl?: string | null;
}

export interface CapabilityDto {
  kind: string;
  label: string;
  icon?: string | null;
  basePath?: string | null;
}

export interface LinkFlowInfoDto {
  id: string;
  label: string;
  description?: string | null;
}

export interface LinkProviderDto {
  id: string;
  displayName: string;
  description?: string | null;
  icon: LinkIconDto;
  capabilities: CapabilityDto[];
  flows: LinkFlowInfoDto[];
  sortOrder: number;
}

export interface LinkOptionDto {
  value: string;
  label: string;
  detail?: string | null;
  icon?: string | null;
  linkProviderId?: string | null;
}

export interface LinkFieldDto {
  key: string;
  label: string;
  type: LinkFieldType;
  placeholder?: string | null;
  help?: string | null;
  defaultValue?: string | null;
  required?: boolean;
  secret?: boolean;
  maxLength?: number | null;
  pattern?: string | null;
  options?: LinkOptionDto[];
  allowCustom?: boolean;
}

export interface LinkActionDto {
  id: string;
  label: string;
}

export interface LinkOtpDto {
  length: number;
  label?: string | null;
  numericOnly?: boolean;
}

export interface LinkPushCodeDto {
  number?: string | null;
  expiresAt: string;
  pollIntervalSeconds: number;
}

export interface LinkDeviceCodeDto {
  userCode: string;
  verificationUri: string;
  expiresAt: string;
  pollIntervalSeconds: number;
}

export interface LinkRedirectDto {
  url: string;
  mode: 'hostCallback' | 'capture';
  callbackPrefix?: string | null;
  captureCookieDomains?: string[];
}

export interface LinkDoneDto {
  accountId: string;
  title?: string | null;
}

export interface LinkStepDto {
  id: string;
  kind: LinkStepKind;
  title?: string | null;
  description?: string | null;
  error?: string | null;
  fields?: LinkFieldDto[];
  otp?: LinkOtpDto | null;
  pushCode?: LinkPushCodeDto | null;
  deviceCode?: LinkDeviceCodeDto | null;
  redirect?: LinkRedirectDto | null;
  options?: LinkOptionDto[];
  actions?: LinkActionDto[];
  submitLabel?: string | null;
  done?: LinkDoneDto | null;
}

export interface LinkSessionDto {
  sessionId: string;
  providerId: string;
  flowId: string;
  status: LinkSessionStatus;
  expiresAt: string;
  step: LinkStepDto;
}

export interface LinkedAccountDto {
  providerId: string;
  providerName: string;
  id: string;
  title: string;
  detail?: string | null;
  status: LinkedAccountStatus;
  message?: string | null;
  linkedAt?: string | null;
  lastSyncedAt?: string | null;
  capabilities: CapabilityDto[];
  canReauth: boolean;
}

export interface LinkStartBody {
  flowId?: string;
  platform: LinkPlatformName;
  reauthAccountId?: string;
}

export interface LinkSubmitBody {
  stepId: string;
  values: Record<string, string>;
  action?: string;
}

const enc = encodeURIComponent;

@Service()
export class LinkingApi {
  private readonly http = inject(HttpClient);

  providers(platform: LinkPlatformName = 'web'): Observable<LinkProviderDto[]> {
    return this.http.get<LinkProviderDto[]>('/api/linking/providers', { params: { platform } });
  }

  start(providerId: string, body: LinkStartBody): Observable<LinkSessionDto> {
    return this.http.post<LinkSessionDto>(`/api/linking/providers/${enc(providerId)}/start`, body);
  }

  submit(sessionId: string, body: LinkSubmitBody): Observable<LinkSessionDto> {
    return this.http.post<LinkSessionDto>(`/api/linking/sessions/${enc(sessionId)}/submit`, body);
  }

  poll(sessionId: string): Observable<LinkSessionDto> {
    return this.http.get<LinkSessionDto>(`/api/linking/sessions/${enc(sessionId)}`);
  }

  cancel(sessionId: string): Observable<void> {
    return this.http.delete<void>(`/api/linking/sessions/${enc(sessionId)}`);
  }

  accounts(): Observable<LinkedAccountDto[]> {
    return this.http.get<LinkedAccountDto[]>('/api/linking/accounts');
  }

  unlink(providerId: string, accountId: string): Observable<void> {
    return this.http.delete<void>(`/api/linking/accounts/${enc(providerId)}/${enc(accountId)}`);
  }
}
