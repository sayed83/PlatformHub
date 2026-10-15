(function (window, document) {
    'use strict';

    function escape(value) {
        return String(value ?? '').replace(/[&<>"']/g, function (character) {
            return ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character];
        });
    }

    function create(options) {
        if (!window.Tabulator) {
            const target = typeof options.element === 'string' ? document.querySelector(options.element) : options.element;
            if (target) target.innerHTML = '<div class="alert alert-warning mb-0">The table library could not load. Refresh the page or check the Tabulator CDN connection.</div>';
            throw new Error('Tabulator is not loaded.');
        }
        const element = typeof options.element === 'string' ? document.querySelector(options.element) : options.element;
        if (!element) throw new Error('PlatformHubGrid target element was not found.');

        const host = element.parentElement;
        let toolbar = null;
        let searchInput = options.searchInput
            ? (typeof options.searchInput === 'string' ? document.querySelector(options.searchInput) : options.searchInput)
            : null;

        if (options.toolbar !== false) {
            toolbar = document.createElement('div');
            toolbar.className = 'ph-grid-toolbar';
            toolbar.innerHTML =
                '<div class="ph-grid-search-wrap"><label class="visually-hidden">Search rows</label>' +
                '<input type="search" class="form-control ph-grid-search" placeholder="' + escape(options.searchPlaceholder || 'Search this list...') + '" aria-label="Search rows"></div>' +
                '<label class="ph-grid-size-wrap"><span>Rows</span><select class="form-select ph-grid-size" aria-label="Rows per page">' +
                '<option value="10">10</option><option value="25">25</option><option value="50">50</option><option value="100">100</option></select></label>';
            host.insertBefore(toolbar, element);
            if (!searchInput) searchInput = toolbar.querySelector('.ph-grid-search');
            const sizeSelect = toolbar.querySelector('.ph-grid-size');
            sizeSelect.value = String(options.paginationSize || 10);
            sizeSelect.addEventListener('change', function () {
                grid.setPageSize(Number(sizeSelect.value));
            });
        }

        const config = {
            data: options.data || [],
            columns: options.columns || [],
            layout: options.layout || 'fitColumns',
            responsiveLayout: options.responsiveLayout || 'hide',
            placeholder: options.placeholder || 'No records found.',
            pagination: options.pagination !== false,
            paginationMode: options.paginationMode || 'local',
            paginationSize: options.paginationSize || 10,
            paginationSizeSelector: false,
            paginationCounter: options.pagination === false ? false : 'rows',
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
        if (searchInput) {
            searchInput.addEventListener('input', function () {
                const query = searchInput.value.trim().toLowerCase();
                if (!query) {
                    grid.clearFilter(true);
                    return;
                }
                grid.setFilter(function (row, params) {
                    return Object.values(row).some(value =>
                        String(value ?? '').toLowerCase().includes(params.query));
                }, { query: query });
            });
        }
        return grid;
    }

    window.PlatformHubGrid = Object.freeze({ create: create, escape: escape });
})(window, document);
