/**
 * Візуалізація структури проекту для швидкого розуміння
 */

export const PROJECT_STRUCTURE = {
    description: "Face Anonymizer Frontend - Модульна архітектура",
    
    modules: {
        css: {
            core: [
                "variables.css - Теми, кольори, CSS змінні",
                "base.css - Reset стилі, базова конфігурація",
                "typography.css - Шрифти, заголовки",
                "layout.css - Сітки, контейнери",
                "utilities.css - Утилітарні класи (.is-hidden, .muted)",
                "footer.css - Стилі футера",
                "responsive.css - Медіа-запити"
            ],
            components: [
                "header.css - Хедер і навігаційні табси",
                "cards.css - Картки та секції",
                "buttons.css - Кнопки всіх типів",
                "forms.css - Інпути, селекти, чекбокси",
                "dropzone.css - Drag & Drop зони",
                "progress.css - Прогрес-бари з анімацією",
                "viewer.css - Перегляд результатів",
                "modal.css - Модальні вікна",
                "kpi.css - Метрики та пілюлі"
            ]
        },
        
        js: {
            core: [
                "main.js - Головний файл, точка входу",
                "config.js - Конфігурація (endpoints, defaults)"
            ],
            services: [
                "api.service.js - HTTP клієнт для backend API",
                "dom.service.js - Управління DOM та глобальним станом"
            ],
            pages: [
                "single.page.js - Логіка одиночного зображення",
                "batch.page.js - Логіка пакетної обробки"
            ],
            components: [
                "dropzone.js - Компоненти Drag & Drop",
                "kpi.js - Рендеринг метрик"
            ],
            utils: [
                "helpers.js - Загальні функції (escapeHtml, uniqueFiles)",
                "theme.js - Управління світлою/темною темою",
                "progress.js - Менеджер прогрес-барів з анімацією",
                "modal.js - Менеджер модальних вікон"
            ],
            legacy: [
                "legacy.js - Шар сумісності зі старим кодом",
                "api.js, dom.js, single.js, batch.js, app.js - Старі файли (deprecated)"
            ]
        }
    },

    principles: {
        separation: "Розділення відповідальностей (Separation of Concerns)",
        modularity: "Модульна структура для легкої підтримки",
        reusability: "Багаторазове використання компонентів",
        scalability: "Легко додавати нові функції",
        maintainability: "Зрозуміла структура для команди"
    }
};

console.log("📦 Project Structure:", PROJECT_STRUCTURE);
