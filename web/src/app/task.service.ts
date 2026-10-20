import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface TaskItem {
  id: number;
  title: string;
  done: boolean;
}

// Ruta relativa: en desarrollo la reenvía proxy.conf.json al backend (localhost:5000);
// en Docker la reenviará nginx al contenedor "api". El código no cambia en ningún caso.
const API = '/api/tasks';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private http = inject(HttpClient);

  list(): Observable<TaskItem[]> {
    return this.http.get<TaskItem[]>(API);
  }

  add(title: string): Observable<TaskItem> {
    return this.http.post<TaskItem>(API, { title });
  }

  toggle(id: number): Observable<TaskItem> {
    return this.http.put<TaskItem>(`${API}/${id}/toggle`, {});
  }

  remove(id: number): Observable<void> {
    return this.http.delete<void>(`${API}/${id}`);
  }
}
