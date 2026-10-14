(function (window) {
    'use strict';

    function escape(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (character) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character];
        });
    }

    function create(options) {
        if (!window.Tabulator) {
            throw new Error('Tabulator is not loaded. Check the shared layout script reference.');
        }
        const element = typeof options.element === 'string'
            ? document.querySelector(options.element)
            : options.element;
        if (!element) throw new Error('PlatformHubGrid target element was not found.');

        const config = {
            data: options.data || [],
            columns: options.columns || [],
            layout: options.layout || 'fitColumns',
            responsiveLayout: options.responsiveLayout || 'hide',
            placeholder: options.placeholder || 'No records found.',
            pagination: options.pagination !== false,
            paginationMode: options.paginationMode || 'local',
            paginationSize: options.paginationSize || 10,
            paginationSizeSelector: options.paginationSizeSelector || [10, 25, 50, 100],
            movableColumns: options.movableColumns === true,
            columnDefaults: { tooltip: true, headerSort: true, ...options.columnDefaults }
        };

        if (options.height) config.height = options.height;
        if (options.ajaxURL) {
            config.ajaxURL = options.ajaxURL;
            config.ajaxConfig = { method: 'GET', credentials: 'same-origin' };
            config.ajaxResponse = function (_url, _params, response) {
                if (Array.isArray(response)) return response;
                if (response && Array.isArray(response.data)) return response.data;
                throw new Error('The list endpoint returned an unsupported data shape.');
            };
        }

        const grid = new window.Tabulator(element, config);
        if (options.searchInput) {
            const input = typeof options.searchInput === 'string'
                ? document.querySelector(options.searchInput)
                : options.searchInput;
            input?.addEventListener('input', function () {
                const query = input.value.trim();
                if (!query) {
                    grid.clearFilter(true);
                    return;
                }
                grid.setFilter(function (row, params) {
                    return Object.values(row).some(value =>
                        String(value ?? '').toLowerCase().includes(params.query));
                }, { query: query.toLowerCase() });
            });
        }
        return grid;
    }

    window.PlatformHubGrid = Object.freeze({ create: create, escape: escape });
})(window);
