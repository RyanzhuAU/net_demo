import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';
import { ApiClient } from '../../../core/api/api-client';
import type {
  CreateTaskItemRequest,
  PagedResultDto,
  ProcessTaskItemRequest,
  ServiceBusTaskMessageDto,
  TaskItemDto,
  TaskQueryParameters,
  UpdateTaskItemRequest,
} from '../../../core/models';

@Injectable({ providedIn: 'root' })
export class TaskApiService {
  private readonly api = inject(ApiClient);

  list(query: TaskQueryParameters): Observable<PagedResultDto<TaskItemDto>> {
    return this.api.get<PagedResultDto<TaskItemDto>>('tasks', {
      status: query.status,
      pageNumber: query.pageNumber,
      pageSize: query.pageSize,
      search: query.search,
    });
  }

  getById(id: string): Observable<TaskItemDto> {
    return this.api.get<TaskItemDto>(`tasks/${id}`);
  }

  create(request: CreateTaskItemRequest): Observable<TaskItemDto> {
    return this.api.post<TaskItemDto, CreateTaskItemRequest>('tasks', request);
  }

  update(id: string, request: UpdateTaskItemRequest): Observable<TaskItemDto> {
    return this.api.put<TaskItemDto, UpdateTaskItemRequest>(`tasks/${id}`, request);
  }

  remove(id: string): Observable<void> {
    return this.api.delete<void>(`tasks/${id}`);
  }

  requestProcessing(id: string, request: ProcessTaskItemRequest): Observable<ServiceBusTaskMessageDto> {
    return this.api.post<ServiceBusTaskMessageDto, ProcessTaskItemRequest>(`tasks/${id}/process`, request);
  }
}
