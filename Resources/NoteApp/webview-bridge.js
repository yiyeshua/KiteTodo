// ============================================================================
// webview-bridge.js — WebView2 interop shim for Tauri v2 APIs
//
// This script runs BEFORE the AINote React app loads.
// It intercepts Tauri's window.__TAURI_INTERNALS__ and redirects all IPC calls
// to window.chrome.webview.postMessage(), enabling the app to run inside
// KiteTodo's WebView2 control without modification.
// ============================================================================

(function () {
  'use strict';

  var pendingCallbacks = {};
  var callbackIdCounter = 0;

  // --------------------------------------------------------------------------
  // Low-level invoke: sends command to .NET host, returns Promise
  // --------------------------------------------------------------------------
  function webviewInvoke(cmd, args) {
    return new Promise(function (resolve, reject) {
      var id = ++callbackIdCounter;
      pendingCallbacks[id] = { resolve: resolve, reject: reject };

      // Timeout after 30 seconds to prevent hanging
      var timeout = setTimeout(function () {
        if (pendingCallbacks[id]) {
          pendingCallbacks[id].reject(new Error('IPC timeout: ' + cmd));
          delete pendingCallbacks[id];
        }
      }, 30000);

      // Wrap to clear timeout on completion
      var originalResolve = pendingCallbacks[id].resolve;
      var originalReject = pendingCallbacks[id].reject;
      pendingCallbacks[id].resolve = function (val) {
        clearTimeout(timeout);
        originalResolve(val);
      };
      pendingCallbacks[id].reject = function (err) {
        clearTimeout(timeout);
        originalReject(err);
      };

      try {
        window.chrome.webview.postMessage(JSON.stringify({
          type: 'invoke',
          id: id,
          cmd: cmd,
          args: args || {}
        }));
      } catch (e) {
        clearTimeout(timeout);
        pendingCallbacks[id].reject(e);
        delete pendingCallbacks[id];
      }
    });
  }

  // --------------------------------------------------------------------------
  // Shim Tauri's internal invoke mechanism
  // Tauri v2 uses window.__TAURI_INTERNALS__.invoke()
  // --------------------------------------------------------------------------
  window.__TAURI_INTERNALS__ = {
    invoke: function (cmd, args, options) {
      return webviewInvoke(cmd, args);
    },

    // convertFileSrc is used for displaying local images
    // In Tauri this converts a filesystem path to an asset:// URL
    // In WebView2 we use the kitenote.local virtual host mapped to the work directory.
    // The input may be an absolute path or already a relative path.
    convertFileSrc: function (filePath) {
      // If it's an absolute path, try to make it relative to the work dir
      // The kitenote.local host maps to the notes work directory
      return 'https://kitenote.local/' + encodeURIComponent(filePath.replace(/\\/g, '/'));
    },

    // Plugin support
    plugin: function (plugin, method, args) {
      return webviewInvoke('plugin:' + plugin + '|' + method, args);
    }
  };

  // Plugin system support
  window.__TAURI_INTERNALS__.plugins = window.__TAURI_INTERNALS__.plugins || {};
  window.__TAURI_INTERNALS__.plugins.path = {
    sep: '/',
    delimiter: ':'
  };

  // --------------------------------------------------------------------------
  // Shim other Tauri APIs that use __TAURI_INTERNALS__
  // --------------------------------------------------------------------------

  // path.join - simple path joining
  window.__TAURI_INTERNALS__.path = {
    join: function () {
      var parts = [];
      for (var i = 0; i < arguments.length; i++) {
        parts.push(arguments[i]);
      }
      return Promise.resolve(parts.join('/').replace(/\/+/g, '/'));
    }
  };

  // event system (mock for now)
  var eventListeners = {};
  window.__TAURI_INTERNALS__.event = {
    listen: function (event, handler) {
      if (!eventListeners[event]) eventListeners[event] = [];
      eventListeners[event].push(handler);
      return Promise.resolve(function () {
        var idx = eventListeners[event].indexOf(handler);
        if (idx >= 0) eventListeners[event].splice(idx, 1);
      });
    },
    emit: function (event, payload) {
      // Forward to .NET for potential handling
      try {
        window.chrome.webview.postMessage(JSON.stringify({
          type: 'event',
          event: event,
          payload: payload
        }));
      } catch (e) { /* ignore */ }
      return Promise.resolve();
    }
  };

  // Emit events from .NET to JS listeners
  window.__tauriEmit = function (event, payload) {
    var handlers = eventListeners[event] || [];
    handlers.forEach(function (h) { try { h(payload); } catch (e) {} });
  };

  // --------------------------------------------------------------------------
  // Listen for responses from .NET host
  // --------------------------------------------------------------------------
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', function (e) {
      try {
        var msg = typeof e.data === 'string' ? JSON.parse(e.data) : e.data;
        if (msg.type === 'invoke_result' && pendingCallbacks[msg.id]) {
          if (msg.error) {
            pendingCallbacks[msg.id].reject(new Error(msg.error));
          } else {
            pendingCallbacks[msg.id].resolve(msg.result);
          }
          delete pendingCallbacks[msg.id];
        } else if (msg.type === 'emit_event') {
          window.__tauriEmit(msg.event, msg.payload);
        }
      } catch (ex) {
        console.error('[webview-bridge] failed to parse message:', ex);
      }
    });
  }

  // --------------------------------------------------------------------------
  // Mock clipboard API (Tauri plugin)
  // --------------------------------------------------------------------------
  window.__TAURI_INTERNALS__.clipboard = {
    writeText: function (text) {
      return webviewInvoke('clipboard_write', { text: text });
    }
  };

  console.log('[webview-bridge] initialized, waiting for app load');
})();
