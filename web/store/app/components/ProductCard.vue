<script setup lang="ts">
// Verbatim port of reference/old/tfoodies/Views/Shared/_PartialProduct.cshtml.
// Same DOM/classes (.promote-sliding .product-single .zoom-wrap .sale …) for legacy main.css.
// Pricing logic preserved: fixprice > price → 特價 (strike original); else plain price.
// (The legacy isset+date<2019-02-01 branch is dead — date已過 — so it folds into the else.)
interface Product {
  title: string
  entitle?: string
  capacity?: string
  photo?: string
  price: number
  fixprice: number
  isset?: boolean
}

const props = withDefaults(defineProps<{
  product: Product
  blobUrl?: string
  promoteSliding?: boolean
}>(), { blobUrl: '', promoteSliding: false })

const href = computed(() => `/Product/${titleToUrlSlug(props.product.title)}`)
const ntd = (n: number) => 'NT. ' + new Intl.NumberFormat('en-US').format(Math.trunc(n))
const onSale = computed(() => props.product.fixprice > props.product.price)
</script>

<template>
  <div :class="promoteSliding ? 'promote-sliding' : ''" class="product-single centered">
    <div class="zoom-wrap">
      <div class="product-pic">
        <a :href="href" :title="product.title"><img :src="blobUrl + (product.photo ?? '')" :alt="product.title"></a>
      </div>
      <a :href="href" class="product-title" :title="product.title">
        <div class="title-inner">
          <div class="title-ch">{{ product.title }}</div>
          <div class="title-en">{{ product.entitle }}</div>
          <p>{{ product.capacity }}</p>
        </div>
      </a>
      <div v-if="onSale" class="sale">
        <div class="inline line-through">原價 {{ ntd(product.fixprice) }}</div>
        <div class="inline sale-tag">特價</div><a :href="href" class="inline price" :title="product.title">{{ ntd(product.price) }}</a>
      </div>
      <div v-else class="sale">
        <a :href="href" class="inline price" :title="product.title">{{ ntd(product.price) }}</a>
      </div>
    </div>
  </div>
</template>
