<script setup lang="ts">
// Home "/" — verbatim port of reference/old/tfoodies/Views/MainMs/Index.cshtml.
// Same DOM/classes for legacy main.css; ViewBag foreach → v-for over useHomeData().
useHead({ title: '首頁' })
const { data } = useHomeData()
</script>

<template>
  <main id="main">
    <div class="owl-carousel owl-theme">
      <template v-for="(banner, i) in data.banners" :key="i">
        <div v-if="banner.style === 1" :style="{ backgroundImage: `url(${data.blobUrl}${banner.photo})` }" class="item-video">
          <a :href="banner.url" target="_blank"></a>
        </div>
        <div v-else-if="banner.style === 2" :style="{ backgroundImage: `url(${data.blobUrl}${banner.photo})` }" class="item-video">
          <img src="/content/images/banner/blank.png">
          <div class="banner centered">
            <div class="banner-inner">
              <div class="index-title white">{{ banner.title }}</div>
              <div class="line"></div><a :href="banner.url" class="btn yellow" target="_blank">進入購買</a>
            </div>
          </div>
        </div>
        <div v-else-if="banner.style === 3" :style="{ backgroundImage: `url(${data.blobUrl}${banner.photo})` }" class="item-video">
          <img src="/content/images/banner/blank.png">
          <div class="banner-text-wrapper">
            <div class="banner-text">{{ banner.title }}</div>
            <h3>{{ banner.subtitle }}</h3>
          </div><a :href="banner.url" target="_blank"></a>
        </div>
        <div v-else-if="banner.style === 4" data-merge="1" class="item-video"><a :href="banner.url" class="owl-video"></a></div>
      </template>
    </div>

    <section v-if="data.latestNews.length" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="index-news-wrap">
          <div class="index-news">
            <a href="/News" title="最新消息" class="title">最新消息</a>
            <div class="direct-line"></div>
            <div class="news-wrapper">
              <div class="clr">
                <a v-for="n in data.latestNews" :key="n.id" :href="`/NewsDetail/${n.id}/1`" class="blog-wrap news-blog-wrap">
                  <div class="article-single centered">
                    <div class="descript left">{{ n.date }}</div>
                    <div class="article-pic"><img :src="data.blobUrl + (n.photo ?? '')" :alt="n.title"></div>
                    <div class="article-title">{{ n.title }}</div>
                  </div>
                </a>
              </div>
            </div>
          </div>
          <div class="index-activity">
            <a href="/Events" title="活動花絮" class="title">活動花絮</a>
            <div class="direct-line"></div>
            <a v-for="e in data.latestEvents" :key="e.id" :href="`/Events`" class="blog-wrap news-blog-wrap">
              <div class="article-single centered">
                <div class="descript left">{{ e.date }}</div>
                <div class="article-pic"><img :src="data.blobUrl + (e.photo ?? '')" :alt="e.title"></div>
                <div class="article-title">{{ e.title }}</div>
              </div>
            </a>
          </div>
        </div>
        <div class="index-fb-wrap">
          <div data-href="https://www.facebook.com/trulyfoodies/" data-tabs="timeline" data-small-header="true" data-adapt-container-width="true" data-hide-cover="false" data-show-facepile="false" data-width="500" data-height="380" class="fb-page"></div>
        </div>
      </div>
    </section>

    <section v-if="data.hotProducts.length" class="allpadding clr section">
      <div class="restrict-wide">
        <div class="centered">
          <div class="title">熱銷商品</div>
          <div class="direct-line"></div>
        </div>
        <div class="responsive promoteSlider clr">
          <ProductCard v-for="(p, i) in data.hotProducts" :key="i" :product="p" :blob-url="data.blobUrl" :promote-sliding="true" />
        </div>
      </div>
    </section>

    <section class="video-section gray-bg">
      <div class="restrict-wide allpadding">
        <div class="centered">
          <a href="/Recipes"><h2>食在呼料理</h2></a>
          <h3>隔週一持續更新最新菜單，一起和我們煮出健康料理吧!</h3>
        </div>
        <div class="video-wrap">
          <div class="main-player">
            <div class="video-slider-for">
              <template v-for="r in data.recipes" :key="r.recipeid">
                <div v-if="!r.v"><a :href="`/Recipe/${r.recipeid}/1`"><img :src="data.blobUrl + (r.photo ?? '')" :alt="r.title"></a></div>
                <div v-else>
                  <iframe :src="`https://www.youtube.com/embed/${r.v}`" frameborder="0" allowfullscreen class="youtubeVideo"></iframe>
                </div>
              </template>
            </div>
          </div>
          <div class="video-select-wrapper">
            <a href="/Recipes"><h2>食在呼料理</h2></a>
            <h3>隔週一持續更新最新菜單，一起和我們煮出健康料理吧!</h3>
            <div class="video-slider-nav">
              <div v-for="r in data.recipes" :key="r.recipeid">
                <div class="video-pic"><img :src="data.blobUrl + (r.rphoto ?? '')" :alt="r.title"></div>
                <div class="video-title"><a href="javascript:;" class="small descript">{{ r.title }}</a></div>
              </div>
            </div>
            <a href="/Recipes" class="descript space">more...   </a>
          </div>
        </div>
      </div>
    </section>

    <section class="allpadding clr section">
      <div class="restrict-wide">
        <div class="centered">
          <h2>最新商品</h2>
          <div>
            <div class="direct-line"></div>
            <div class="tabber-contents">
              <div class="tabber-content active">
                <div class="responsive promoteSlider clr">
                  <ProductCard v-for="(p, i) in data.latestProducts" :key="i" :product="p" :blob-url="data.blobUrl" :promote-sliding="true" />
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="centered more"><a href="/Products" class="outline-btn">More</a></div>
      </div>
    </section>

    <section class="main-bg subscribe-section">
      <div class="subscribe centered">
        <p class="white">GET FOOD INSPIRATION, NEWS AND GOOD OFFERS</p>
        <div class="title ci-title white">TFOODIES<br>PUT PASSION INTO FOOD</div><a href="/TFoodies" class="outline-btn white-btn-line">關於我們</a>
      </div>
      <div class="member-pic"><img src="/content/images/section/memer.png"></div>
    </section>

    <section class="section allpadding clr section">
      <div class="restrict-wide centered">
        <h2>綠誌</h2>
        <p class="content">食在呼每週更新一篇綠誌，與大家分享各種健康知識，大公開橄欖油相關的飲食保健、美容健身、居家生活、身心靈等文章，讓我們一起為踏上健康人生！</p>
        <div class="three-column clr">
          <a v-for="(issue, i) in data.latestIssues" :key="i" :href="`/Issue/${titleToUrlSlug(issue.title)}/1`" class="blog-wrap">
            <div class="article-single centered">
              <div class="article-pic"><img :src="data.blobUrl + (issue.photo ?? '')" :alt="issue.title"></div>
              <div class="article-title">{{ issue.title }}</div>
            </div>
          </a>
        </div>
      </div>
    </section>
  </main>
</template>
