"""OpenLP-side bridge for the ScriptureSync desktop utility."""

import logging

from PyQt5 import QtCore, QtWidgets

from openlp.core.common.i18n import translate
from openlp.core.lib.plugin import Plugin, PluginStatus, StringContent
from openlp.core.lib.ui import create_action
from openlp.core.state import State

from .bridge import LocalBridgeServer


log = logging.getLogger(__name__)


class OpenLPRequestQueue(QtCore.QObject):
    """Execute localhost bridge operations on OpenLP's Qt UI thread."""

    bridge_request_received = QtCore.pyqtSignal(object)

    def __init__(self, plugin):
        super(OpenLPRequestQueue, self).__init__(plugin.main_window)
        self.plugin = plugin
        self.bridge_request_received.connect(
            self._process_bridge_request,
            type=QtCore.Qt.QueuedConnection)

    def enqueue_bridge_request(self, request):
        self.bridge_request_received.emit(request)

    def get_bible_manager(self):
        bible_plugin = self.plugin.plugin_manager.get_plugin_by_name('bibles')
        if bible_plugin is None or bible_plugin.manager is None:
            raise RuntimeError('The OpenLP Bibles plugin is not available.')
        return bible_plugin.manager

    def lookup_scripture(self, bible_name, reference):
        bible_manager = self.get_bible_manager()
        if bible_name not in bible_manager.get_bibles():
            raise ValueError('The selected Bible is no longer installed: {0}'.format(bible_name))
        parsed_reference = bible_manager.parse_ref(bible_name, reference)
        if not parsed_reference:
            raise ValueError('OpenLP could not parse the reference: {0}'.format(reference))
        verses = bible_manager.get_verses(bible_name, parsed_reference, False)
        if not verses:
            raise ValueError(
                'OpenLP found no verses for {0} in {1}.'.format(reference, bible_name))
        return bible_manager, verses

    def add_verses_to_service(self, bible_manager, bible_name, verses):
        """Build, add, and confirm one native OpenLP Bible service item."""
        bible_plugin = self.plugin.plugin_manager.get_plugin_by_name('bibles')
        bible_media_item = bible_plugin.media_item
        if bible_media_item is None:
            raise RuntimeError('The OpenLP Bible media item is not available.')

        def metadata_value(key):
            metadata = bible_manager.get_meta_data(bible_name, key)
            return metadata.value if metadata else ''

        version = metadata_value('name')
        copyright_text = metadata_value('copyright')
        permissions = metadata_value('permissions')
        language_selection = bible_manager.get_language_selection(bible_name)
        display_rows = []
        for verse in verses:
            book_name = verse.book.get_name(language_selection)
            display_rows.append({
                'book': book_name,
                'chapter': verse.chapter,
                'verse': verse.verse,
                'bible': bible_name,
                'version': version,
                'copyright': copyright_text,
                'permissions': permissions,
                'text': verse.text,
                'second_bible': '',
                'second_version': '',
                'second_copyright': '',
                'second_permissions': '',
                'second_text': '',
                'item_title': '{0} {1}:{2} ({3})'.format(
                    book_name, verse.chapter, verse.verse, version)
            })

        list_items = bible_media_item.build_list_widget_items(display_rows)
        service_manager = bible_media_item.service_manager
        before_count = len(service_manager.service_items)
        before_item_ids = {
            id(item['service_item']) for item in service_manager.service_items
        }
        bible_media_item.add_to_service(item=list_items)
        if len(service_manager.service_items) != before_count + 1:
            raise RuntimeError(
                'OpenLP did not confirm that the scripture was added to the service.')
        added_items = [
            item['service_item']
            for item in service_manager.service_items
            if id(item['service_item']) not in before_item_ids
        ]
        if len(added_items) != 1:
            raise RuntimeError(
                'OpenLP changed the service, but ScriptureSync could not identify the new item.')
        return added_items[0].title

    @QtCore.pyqtSlot(object)
    def _process_bridge_request(self, request):
        try:
            if request.operation == 'list_bibles':
                bible_names = sorted(
                    self.get_bible_manager().get_bibles().keys(),
                    key=str.casefold)
                request.succeed({'bibles': bible_names})
                return

            bible_name = request.payload['bible']
            reference = request.payload['reference']
            bible_manager, verses = self.lookup_scripture(bible_name, reference)
            if request.operation == 'search_scripture':
                request.succeed({
                    'reference': reference,
                    'bible': bible_name,
                    'verse_text': ' '.join(verse.text for verse in verses)
                })
            elif request.operation == 'add_scripture':
                title = self.add_verses_to_service(
                    bible_manager, bible_name, verses)
                request.succeed({
                    'reference': reference,
                    'bible': bible_name,
                    'service_item_title': title,
                    'added': True
                })
            else:
                raise ValueError(
                    'Unknown ScriptureSync operation: {0}'.format(request.operation))
        except Exception as error:
            log.exception('ScriptureSync localhost bridge request failed')
            request.fail(str(error))


class ScripturesyncPlugin(Plugin):
    """Safely connect the ScriptureSync desktop utility to OpenLP."""

    def __init__(self):
        super(ScripturesyncPlugin, self).__init__('scripturesync', version='0.2.0')
        self.weight = 120
        self.status_action = None
        self.request_queue = OpenLPRequestQueue(self)
        self.bridge_server = LocalBridgeServer(
            self.request_queue.enqueue_bridge_request)
        self.bridge_error = None
        self.settings.extend_default_settings({
            'scripturesync/status': PluginStatus.Inactive
        })
        State().add_service(self.name, self.weight, is_plugin=True)
        State().update_pre_conditions(self.name, self.check_pre_conditions())
        log.info('ScriptureSync plugin loaded')

    def add_tools_menu_item(self, tools_menu):
        self.status_action = create_action(
            tools_menu,
            'toolsScriptureSyncStatus',
            text=translate('ScriptureSyncPlugin', 'ScriptureSync Status'),
            statustip=translate(
                'ScriptureSyncPlugin',
                'Show the local ScriptureSync bridge status.'),
            visible=False,
            triggers=self.show_status)
        tools_menu.addAction(self.status_action)

    def initialise(self):
        super(ScripturesyncPlugin, self).initialise()
        if self.status_action:
            self.status_action.setVisible(True)
        try:
            self.bridge_server.start()
            self.bridge_error = None
        except Exception as error:
            self.bridge_error = str(error)
            log.exception('ScriptureSync localhost bridge could not start')
        log.info('ScriptureSync plugin activated')

    def finalise(self):
        self.bridge_server.stop()
        if self.status_action:
            self.status_action.setVisible(False)
        log.info('ScriptureSync plugin deactivated')
        super(ScripturesyncPlugin, self).finalise()

    def show_status(self):
        if self.bridge_error:
            QtWidgets.QMessageBox.critical(
                self.main_window,
                translate('ScriptureSyncPlugin', 'ScriptureSync Error'),
                translate(
                    'ScriptureSyncPlugin',
                    'The local bridge could not start:\n\n{error}'
                ).format(error=self.bridge_error))
            return
        try:
            bible_names = self.request_queue.get_bible_manager().get_bibles()
            QtWidgets.QMessageBox.information(
                self.main_window,
                translate('ScriptureSyncPlugin', 'ScriptureSync Status'),
                translate(
                    'ScriptureSyncPlugin',
                    'Ready for the ScriptureSync desktop utility.\n\n'
                    'Local bridge: http://127.0.0.1:4317\n'
                    'Installed Bibles: {count}'
                ).format(count=len(bible_names)))
        except Exception as error:
            QtWidgets.QMessageBox.critical(
                self.main_window,
                translate('ScriptureSyncPlugin', 'ScriptureSync Error'),
                str(error))

    @staticmethod
    def about():
        return translate(
            'ScriptureSyncPlugin',
            '<strong>ScriptureSync</strong><br />'
            'Safely coordinates scripture searches and service additions with OpenLP.')

    def set_plugin_text_strings(self):
        self.text_strings[StringContent.Name] = {
            'singular': translate('ScriptureSyncPlugin', 'ScriptureSync', 'name singular'),
            'plural': translate('ScriptureSyncPlugin', 'ScriptureSync', 'name plural')
        }
        self.text_strings[StringContent.VisibleName] = {
            'title': translate('ScriptureSyncPlugin', 'ScriptureSync', 'container title')
        }
