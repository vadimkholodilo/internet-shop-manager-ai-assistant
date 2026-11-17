<?php
namespace Opencart\Admin\Controller\Extension\InternetShop\Module;
/**
 * Catalog API module: Extensions > Modules registers the extension (see the install() hook of OpenCart's
 * module installer) so the catalog application autoloads it. The page only reports where the API lives.
 */
class CatalogApi extends \Opencart\System\Engine\Controller {
    public function index(): void {
        $this->load->language('extension/internet_shop/module/catalog_api');

        $this->document->setTitle($this->language->get('heading_title'));

        $data['breadcrumbs'] = [];

        $data['breadcrumbs'][] = [
            'text' => $this->language->get('text_home'),
            'href' => $this->url->link('common/dashboard', 'user_token=' . $this->session->data['user_token'])
        ];

        $data['breadcrumbs'][] = [
            'text' => $this->language->get('text_extension'),
            'href' => $this->url->link('marketplace/extension', 'user_token=' . $this->session->data['user_token'] . '&type=module')
        ];

        $data['breadcrumbs'][] = [
            'text' => $this->language->get('heading_title'),
            'href' => $this->url->link('extension/internet_shop/module/catalog_api', 'user_token=' . $this->session->data['user_token'])
        ];

        $data['back'] = $this->url->link('marketplace/extension', 'user_token=' . $this->session->data['user_token'] . '&type=module');
        $data['api_url'] = HTTP_CATALOG . 'catalog-api/';

        $data['header'] = $this->load->controller('common/header');
        $data['column_left'] = $this->load->controller('common/column_left');
        $data['footer'] = $this->load->controller('common/footer');

        $this->response->setOutput($this->load->view('extension/internet_shop/module/catalog_api', $data));
    }
}
