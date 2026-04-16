import { test, expect } from '@playwright/test';


//go to je prazan string jer je baseURL namesten u playwright.config.ts na
//  baseURL: 'http://localhost:5173/'

//1.
//provarava da li se vidi tekst Pronadji uslugu
test('Vidi tekst', async ({ page }) => {
  await page.goto('');
  await expect (page.getByRole('heading', {name:"Pronađi uslugu"})).toBeVisible();
});


//2.
//klikne na link koji vodi na glavnoj stranicu i proverava da li je stigao preko teksta
test('Main page link', async ({ page }) => {
  await page.goto('');

  await page.getByRole('link', { name: 'NearYou' }).click();

  await expect(page.getByRole('heading', { name: 'Pronađi uslugu' })).toBeVisible();
});


//3.
//menjamo kategoriju oglasa u Elektronika
//proverava se da li se promenio tekst u tom box-u, ne i da li su stvarno ti oglasi pojavili
test ('Promena kategorije', async({page})=> {
  await page.goto('');

// nadji div koji ima label u kome pise Kategorija
//zatim unutar tog nadjenog diva >>
//trazi select
  const kategorijaDropdown = page.locator('div:has(> label:text("Kategorija")) >> select');

  await kategorijaDropdown.selectOption('Elektronika');
  //ovde moze ovako jer je value postavaljena na Kategorija.naziv, ispod za sortiranje je broj samo
  //proverava dal je to selektovano
  await expect(kategorijaDropdown).toHaveValue('Elektronika');


});

//4.
//sortira oglase po rastucoj ceni i proverava da li se selektovala ta opcija
test ('Sortiranje oglasa',async({page}) => {
  await page.goto('');

    const sortiranjeDropdown = page.locator('div:has(> label:text("Sortiranje")) >> select');

   await sortiranjeDropdown.selectOption({label:'Cena (Rastuća)'});
  
    // lakse ovako ali neprakticno jer moramo znati kako je u backendu
    // await expect(sortiranjeDropdown).toHaveValue('1');

    // proverava da li unutar selekta checked/selektovana opcija sa textom 
   await expect(sortiranjeDropdown.locator('option:checked')).toHaveText('Cena (Rastuća)');
});




//5.
test ('Registracija postojeceg korisnika',async({page}) => {
  await page.goto('');
  
  await expect (page.getByRole('link', {name:"Registrujte se"})).toBeVisible();
  await page.getByRole('link', { name: 'Registrujte se' }).click();
  await expect (page.getByRole('heading', {name:"Register"})).toBeVisible();

  //sigurno smo dosli do registracije
  await page.getByPlaceholder('Enter name').fill('TestTest');
 // await expect (page.getByText('TestTest')).toBeVisible();
  await page.getByLabel('Surname').fill('Testic123');

  await page.getByPlaceholder('Enter username').fill('UsernameTest');
  await page.getByPlaceholder('Enter email').fill('TestTest123@gmail.com');
  await page.getByPlaceholder('Enter telephone').fill('1234456');
  await page.getByPlaceholder('Enter skill1, skill2, skill3...').fill('Majstor za struju');
  //await page.getByLabel('Skills (Odvojiti zarezom)').fill('Vodoinstalater, Majstor za struju');
  await page.getByPlaceholder('Enter password').fill('TestTest123!');
  await page.getByPlaceholder('Confirm password').fill('TestTest123!');

  await page.getByRole('button',{name:'Register'}).click();

  //await expect(page.getByText(/Username već postoji/i)).toBeVisible();
  await expect(page).toHaveURL(/.*register/); 
  // samo ovo se proverava, nema vidljive poruke jer je radjeno sa alert(), pa se iskace prozor
  //koji playground automatski zatvara, msm moze da se napravi da ih ocekuje ali nema potrebe


});

//6.
test ('Registracija novog korisnika',async({page}) => {
  await page.goto('');
  
  const unikatanBroj = Date.now();
  const username = `user_${unikatanBroj}`;
  const email = `test_${unikatanBroj}@gmail.com`;

  await expect (page.getByRole('link', {name:"Registrujte se"})).toBeVisible();
  await page.getByRole('link', { name: 'Registrujte se' }).click();
  await expect (page.getByRole('heading', {name:"Register"})).toBeVisible();


  await page.getByPlaceholder('Enter name').fill('TestTest');
  await page.getByLabel('Surname').fill('Testic123');
  await page.getByPlaceholder('Enter username').fill(username);
  await page.getByPlaceholder('Enter email').fill(email);
  await page.getByPlaceholder('Enter telephone').fill('1234456');
  await page.getByPlaceholder('Enter skill1, skill2, skill3...').fill('Majstor za struju');
  //await page.getByLabel('Skills (Odvojiti zarezom)').fill('Vodoinstalater, Majstor za struju');
  await page.getByPlaceholder('Enter password').fill('TestTest123!');
  await page.getByPlaceholder('Confirm password').fill('TestTest123!');

  await page.getByRole('button',{name:'Register'}).click();
  await expect(page).toHaveURL(/.*login/);
  await expect (page.getByRole('heading', {name:"Login"})).toBeVisible();
  //await expect(page.getByText(/Username već postoji/i)).toBeVisible();
});

//7.
test ('Login',async({page}) => {
  await page.goto('');


  await expect (page.getByRole('link', {name:"Prijavite se"})).toBeVisible();
  await page.getByRole('link', { name: 'Prijavite se' }).click();
  await expect (page.getByRole('heading', {name:"Login"})).toBeVisible();

  await page.getByPlaceholder('Enter username').fill('UsernameTest');
  await page.getByPlaceholder('Enter password').fill('TestTest123!');

   await page.getByRole('button',{name:'Login'}).click();
  // await expect(page).toHaveURL(/./);
   await expect (page.getByRole('heading', {name:"Pronađi uslugu"})).toBeVisible();

});


//8.
test ('Promena sa login na Register i obrnuto',async({page}) => {
  await page.goto('');

  await page.getByRole('link', { name: 'Prijavite se' }).click();
  await expect (page.getByRole('heading', {name:"Login"})).toBeVisible();

  await page.locator('#login-form').getByRole('link', { name: 'Registrujte se' }).click();
  await expect (page.getByRole('heading', {name:"Register"})).toBeVisible();

  await page.getByRole('link', { name: 'Ulogujte se' }).click();
  await expect (page.getByRole('heading', {name:"Login"})).toBeVisible();
});


//9
test('Klik na prvi oglas ', async ({ page }) => {
  await page.goto('/');

  //proverava se naslov oglasa na main-u i posle kad se klikne na oglas da je isti

  const prviOglas = page.locator('.oglas-card').first(); 
  const naslovOglasa = await prviOglas.locator('h3').textContent();
  await prviOglas.click();

  await expect(page.getByText("Prijavite se da biste narucili oglas")).toBeVisible();
  await expect(page.locator('h1')).toContainText(naslovOglasa ?? "");
});

//10.
test ('Klik na autora oglasa',async({page}) => {
  await page.goto('/');


  const prviOglas = page.locator('.oglas-card').first(); 

  await prviOglas.click();

  await expect(page.getByText("Prijavite se da biste narucili oglas")).toBeVisible();

  await page.getByRole('link', { name: 'Član od' }).click();
  //najlakse ovako, jer uvek postoji na svakom username-u

  await expect(page).toHaveURL(/.*login/);
  await expect (page.getByRole('heading', {name:"Login"})).toBeVisible();
});

//11.
test ('Pretraga oglasa po nazivu',async({page}) => {
  await page.goto('/');

  await page.getByPlaceholder("Npr. 'telefon'").fill('stan');
  await page.getByRole("button",{name:"Pretraži"}).click();

  const prvaKartica = page.locator('.oglas-card').first();
  await expect(prvaKartica).toBeVisible({ timeout: 10000 }); // Čeka do 10s ako je server spor

  await prvaKartica.click();
 
  await expect(page.locator('.modal-content')).toContainText(/stan/i);
  //Pretrazujemo ceo modal jer nam pretraga pokazuje i ako postoji ta rec/deo reci u opisu oglasa
   
 
});

//12.
test('Provera oglasa preko mape', async ({ page }) => {
  await page.goto('/');
  await page.context().grantPermissions(['geolocation']);
  await page.context().setGeolocation({ latitude: 43.3209, longitude: 21.8958 }); // Koordinate Niša, npr.

  await page.getByRole("button", { name: "Mapa" }).click();
  await page.getByRole('button', { name: 'Zoom in' }).click();

  const prviMarker = page.locator('.marker-container').first();
  await prviMarker.locator('.marker-pill').hover({ force: true }); 

  const nazivOglasa = await prviMarker.locator('.marker-tooltip').textContent();
  
  await prviMarker.locator('.marker-pill').click({ force: true }); 

  await expect(page.getByRole('heading', { name: "Opis oglasa" })).toBeVisible();
  await expect(page.locator('.modal-content h1')).toContainText(nazivOglasa ?? "");
});