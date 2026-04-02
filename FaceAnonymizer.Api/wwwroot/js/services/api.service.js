/**
 * API клієнт для взаємодії з backend
 */

import { config } from '../config.js';

export class ApiClient {
    /**
     * Завантаження можливостей системи (engines, methods)
     */
    async loadCapabilities() {
        const res = await fetch(config.api.endpoints.capabilities);
        if (!res.ok) throw new Error("HTTP " + res.status);
        return await res.json();
    }

    /**
     * Відправка multipart/form-data запиту з файлом
     */
    async postMultipart(url, file) {
        if (!file) throw new Error("Оберіть файл зображення.");

        const fd = new FormData();
        fd.append("image", file);

        const res = await fetch(url, { method: "POST", body: fd });
        const text = await res.text();

        let json = {};
        try {
            json = text ? JSON.parse(text) : {};
        } catch {
            json = { raw: text };
        }

        if (!res.ok) {
            throw new Error(json?.error || json?.title || ("HTTP " + res.status));
        }
        return json;
    }

    /**
     * Завантаження пакету файлів
     */
    async uploadBatch(runId, files) {
        if (!files?.length) throw new Error("Немає файлів для завантаження.");

        const fd = new FormData();
        if (runId) fd.append("runId", runId);

        files.forEach(f => {
            fd.append("files", f, f.name);
            const rel = f.webkitRelativePath || f.name;
            fd.append("paths", rel);
        });

        const res = await fetch(config.api.endpoints.batchUpload, {
            method: "POST",
            body: fd
        });
        
        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json?.error || ("HTTP " + res.status));
        return json;
    }

    /**
     * Запуск пакетної обробки
     */
    async runBatch(body) {
        const res = await fetch(config.api.endpoints.batchRun, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body)
        });

        const json = await res.json().catch(() => ({}));
        if (!res.ok) throw new Error(json?.error || ("HTTP " + res.status));
        return json;
    }

    /**
     * Отримання списку вихідних файлів
     */
    async listOutputs(runId, kind) {
        const url = `${config.api.endpoints.batchOutputs}?runId=${encodeURIComponent(runId)}&kind=${encodeURIComponent(kind)}`;
        const res = await fetch(url);
        const json = await res.json().catch(() => []);
        if (!res.ok) throw new Error(json?.error || ("HTTP " + res.status));
        return Array.isArray(json) ? json : [];
    }

    /**
     * Побудова URL для вихідного файлу
     */
    buildOutputUrl(runId, relPath) {
        if (!relPath) return null;
        return `${config.api.endpoints.batchOutputFile}?runId=${encodeURIComponent(runId)}&path=${encodeURIComponent(relPath)}`;
    }

    /**
     * Побудова URL для PDF звіту
     */
    buildReportPdfUrl(runId, inline = true) {
        return `${config.api.endpoints.batchReportPdf}?runId=${encodeURIComponent(runId)}&inline=${inline}`;
    }

    /**
     * Побудова URL для ZIP архіву
     */
    buildDownloadZipUrl(runId) {
        return `${config.api.endpoints.batchDownloadZip}?runId=${encodeURIComponent(runId)}`;
    }
}
