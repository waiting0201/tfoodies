/**
 * Google分析 - 加強電子商務
 * Google Analytics Enhanced Econmmerce
 * https://developers.google.com/analytics/devguides/collection/gtagjs/enhanced-ecommerce
 */
var gtag = function () {
    window.dataLayer = window.dataLayer || [];
    function gtag() { dataLayer.push(arguments) };

    var currency = 'TWD';
    var lastStep = 3;

    var ItemData = function (id, name, category, price, quantity) {
        var self = this;
        self.id = id;
        self.name = name;
        self.category = category;
        self.price = price;
        self.quantity = quantity;

        /**
         * 判斷是否有其它參數，有brand、category、variant、price、quantity、coupon、list_name、list_position
         */
        /*if (!isEmpty(params)) {
            self = $.extend(self, params);
        }*/
    };

    var view_item = function (id, name, params) {
        var item = new ItemData(id, name, params);
        gtag('event', 'view_item', { 'items': [item] });
    };

    var add_to_cart = function (id, name, params) {
        var item = new ItemData(id, name, params);
        gtag('event', 'add_to_cart', { 'items': [item] });
    };

    var remove_from_cart = function (id, name, params) {
        var item = new ItemData(id, name, params);
        gtag('event', 'remove_from_cart', { 'items': [item] });
    };

    var begin_checkout = function (itemList, coupon) {
        coupon = !isEmpty(coupon) ? coupon : '';
        gtag('event', 'begin_checkout', { 'items': setItemList(itemList), 'coupon': coupon });
    };

    var checkout_progress = function (itemList, coupon, step, option) {
        coupon = !isEmpty(coupon) ? coupon : '';
        step = !isEmpty(step) ? step : 2;
        option = !isEmpty(option) ? option : '';

        gtag('event', 'checkout_progress', {
            'items': setItemList(itemList),
            'coupon': coupon,
            'checkout_step': step,
            'checkout_option': option
        });
    };

    var purchase = function (itemList, orderData, coupon, option) {
        checkout_progress(itemList, coupon, lastStep, option);

        gtag('event', 'purchase', {
            'transaction_id': orderData['transaction_id'],
            'affiliation': '',
            'value': orderData['value'],
            //'currency': currency,
            'tax': orderData['tax'],
            'shipping': orderData['shipping'],
            'items': setItemList(itemList)
        });
    };

    var refund = function (transaction_id, itemList) {
        if (isEmpty(itemList)) {
            gtag('event', 'refund', { transaction_id: transaction_id });
        } else {
            gtag('event', 'refund', { transaction_id: transaction_id, items: setItemList(itemList) });
        }
    }

    function setItemList(itemList) {
        var items = [];
        $.each(itemList, function (index, item) {
            items.push(new ItemData(item['id'], item['name'], item['category'], item['price'], item['quantity']));
        });

        return items;
    }

    function isEmpty(value) {
        return (value == undefined) || (value == null) || (value == "");
    }

    return {
        view_item: view_item,
        add_to_cart: add_to_cart,
        remove_from_cart: remove_from_cart,
        begin_checkout: begin_checkout,
        checkout_progress: checkout_progress,
        purchase: purchase,
        refund: refund
    };
}();