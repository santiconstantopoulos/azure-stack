import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TaskItem, TaskService } from './task.service';

@Component({
  selector: 'app-root',
  imports: [FormsModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit {
  private service = inject(TaskService);

  tasks = signal<TaskItem[]>([]);
  error = signal<string | null>(null);
  newTitle = '';

  pending = computed(() => this.tasks().filter(t => !t.done).length);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.service.list().subscribe({
      next: tasks => {
        this.tasks.set(tasks);
        this.error.set(null);
      },
      error: () => this.error.set('No se pudo conectar con la API. ¿Está corriendo el backend?')
    });
  }

  add(): void {
    const title = this.newTitle.trim();
    if (!title) return;
    this.service.add(title).subscribe({
      next: task => {
        this.tasks.update(list => [...list, task]);
        this.newTitle = '';
      },
      error: () => this.error.set('No se pudo crear la tarea.')
    });
  }

  toggle(task: TaskItem): void {
    this.service.toggle(task.id).subscribe({
      next: updated => this.tasks.update(list => list.map(t => (t.id === updated.id ? updated : t))),
      error: () => this.error.set('No se pudo actualizar la tarea.')
    });
  }

  remove(task: TaskItem): void {
    this.service.remove(task.id).subscribe({
      next: () => this.tasks.update(list => list.filter(t => t.id !== task.id)),
      error: () => this.error.set('No se pudo borrar la tarea.')
    });
  }
}
