<script setup lang="ts">
// Port of reference/old/tfoodies/Views/MainMs/Blogs.cshtml.
// URL: /Blogs — single non-paginated grid; each card opens an external blog article.
const { data } = await useBlogsData()

useSeo(() => ({
  title: '部落客分享',
  description: '食在呼 TFoodies 部落客分享：精選部落客的橄欖油與天然食材料理心得與推薦。',
  type: 'website',
}))
</script>

<template>
  <main id="main">
    <section class="allpadding clr tallsection">
      <div class="restrict-wide">
        <div class="centered"><h1>部落客分享</h1></div>
        <div class="direct-line"></div>
      </div>
    </section>

    <section class="allpadding">
      <div class="restrict-wide">
        <div class="three-column clr page">
          <div v-for="blog in data.items" :key="blog.blogid" class="blog-wrap">
            <a :href="blog.link" class="article-single centered" target="_blank" rel="nofollow">
              <div class="article-pic">
                <img :src="data.blobUrl + (blog.photo ?? '')" :alt="blog.title">
                <div class="blog-link">
                  <div class="blog-link-inner">
                    <div class="share-link"></div>
                  </div>
                </div>
              </div>
              <div class="article-title">{{ blog.title }}</div>
            </a>
          </div>
        </div>
      </div>
    </section>

    <div class="centered more"><a href="#wrapper" class="outline-btn">TOP</a></div>
  </main>
</template>
