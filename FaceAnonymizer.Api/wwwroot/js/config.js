/**
 * Конфігурація додатку
 */

export const config = {
    api: {
        baseUrl: '',
        endpoints: {
            capabilities: '/api/faces/capabilities',
            detect: '/api/faces/detect',
            anonymize: '/api/faces/anonymize',
            batchUpload: '/api/faces/batch/upload',
            batchRun: '/api/faces/batch',
            batchOutputs: '/api/faces/batch/outputs',
            batchOutputFile: '/api/faces/batch/output-file',
            batchReportPdf: '/api/faces/batch/report.pdf',
            batchDownloadZip: '/api/faces/batch/download.zip'
        }
    },
    
    defaults: {
        engine: 'yunet',
        method: 'GaussianBlur',
        strength: 0.75,
        color: '#000000',
        evaluate: true,
        ioUThreshold: 0.5
    },

    progress: {
        single: {
            detect: { softCeiling: 95, intervalMs: 80, speed: 0.12 },
            anonymize: { softCeiling: 85, intervalMs: 120, speed: 0.07 }
        },
        batch: {
            upload: { softCeiling: 92, intervalMs: 120, speed: 0.10 },
            process: { softCeiling: 70, intervalMs: 300, speed: 0.025 }
        }
    }
};
