<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/RecipeDetail.cshtml.
// URL: /Recipe/{recipeid}/{p?}
const route = useRoute()
const recipeid = computed(() => String(route.params.recipeid ?? ''))
const pageNum = computed(() => Number(route.params.p ?? 1))
const { data } = await useRecipeDetailData(recipeid.value, pageNum.value)
const item = computed(() => data.value.item)

useHead(() => ({ title: item.value?.title ?? '美味料理' }))

let step = 1
</script>

<template>
  <main id="main">
    <section class="restrict-wide allpadding">
      <div class="locate">
        <p>
          <a :href="`/Recipes/${data.pageNumber}`" class="descript">美味料理/</a>
          <a href="javascript:;" class="descript main">{{ item?.title }}</a>
        </p>
      </div>
    </section>

    <section v-if="item" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="recipe-wrap">
          <div class="food">
            <div class="recipe-img">
              <img :src="data.blobUrl + (item.photo ?? '')" :alt="`${item.title}, 橄欖油`">
            </div>
          </div>
          <div class="food-desc">
            <h1>{{ item.title }}</h1>
            <div class="cook-desc-wrap clr">
              <div v-if="item.duration" class="cook-desc">
                <div class="clock inline"></div>
                <p>{{ item.duration }} 分鐘</p>
              </div>
              <div v-if="item.portion" class="cook-desc">
                <div class="pnumber inline"></div>
                <p>{{ item.portion }} 人份</p>
              </div>
              <div v-if="item.youtube" class="cook-desc">
                <a href="javascript:;" class="play inline"></a>
                <p><a :href="item.youtube" target="_blank">線上影片</a></p>
              </div>
            </div>
            <div class="horizon-line"></div>
            <p v-html="(item.intro ?? '').replace(/\n/g, '<br />')"></p>
          </div>
        </div>

        <div class="horizon-line"></div>

        <section>
          <div class="ingredient-wrapper">
            <div class="ingredient-box">
              <table class="ingredient-table">
                <tr><th><h2 class="left">食材準備</h2></th><th></th></tr>
                <tr v-for="ing in item.ingredients" :key="ing.sort">
                  <td class="left">{{ ing.title }}</td>
                  <td>{{ ing.value }}</td>
                </tr>
              </table>
            </div>
            <div class="ingredient-box">
              <table class="ingredient-table">
                <tr><th><h2 class="left">調味料準備</h2></th><th></th></tr>
                <tr v-for="s in item.seasonings" :key="s.sort">
                  <td class="left">{{ s.title }}</td>
                  <td>{{ s.value }}</td>
                </tr>
              </table>
            </div>
          </div>
        </section>

        <section class="allsection">
          <div class="step">
            <h3>製作步驟</h3>
            <article>
              <ul>
                <template v-for="(rs, idx) in item.steps" :key="rs.sort">
                  <li class="steptitle">{{ idx + 1 }}.{{ rs.title }}</li>
                  <li class="stepcontent" v-html="(rs.value ?? '').replace(/\n/g, '<br/>')"></li>
                </template>
              </ul>
            </article>
          </div>
        </section>

        <div class="centered more">
          <a :href="`/Recipes/${data.pageNumber}`" class="outline-btn">返回</a>
        </div>
      </div>
    </section>

    <section v-if="item && item.products.length" class="gray-bg allsection">
      <div class="restrict-wide allpadding">
        <div class="adstitle"><h2 class="main">購買相關商品</h2></div>
        <div class="responsive promoteSlider clr">
          <ProductCard
            v-for="p in item.products.filter(p => !p.isdisabled).sort((a,b) => b.sort - a.sort)"
            :key="p.productid"
            :product="p" :blob-url="data.blobUrl" :promote-sliding="true"
          />
        </div>
      </div>
    </section>
  </main>
</template>
